using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A context over a connection string. Each of its commands opens a connection and closes it when the command is
/// done; <c>UsingConnection</c> and <c>UsingTransaction</c> run several commands on one connection.
/// </summary>
public abstract class ConnectionStringContext : DbContext
{
    /// <summary>
    /// The connection string of the context.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Creates a context over the connection string with copies of the given options.
    /// </summary>
    protected internal ConnectionStringContext(ContextProvider contextProvider, string connectionString,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        this.ConnectionString = connectionString;
    }

    internal override ICommandCreator GetICommandCreator()
    {
        return new WrappedCommandWithContext(this);
    }

    /// <summary>
    /// Creates a connection for the connection string. The connection is not open.
    /// </summary>
    protected internal abstract DbConnection CreateConnection();

    #region UsingConnection

    /// <summary>
    /// Opens a connection, runs the action with a context of the connection and closes the connection.
    /// </summary>
    /// <remarks>
    /// For async work use <c>UsingConnectionAsync</c>. An async lambda written in the call binds to the overload
    /// with a result, which refuses it; an async method passed as this <c>Action</c> would have the connection
    /// closed at its first <c>await</c>.
    /// </remarks>
    public void UsingConnection(Action<ConnectionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var conn = CreateConnection();
        conn.Open();
        action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
    }

    // The connection is closed when the function returns. A query that is returned must have been read
    // (ToArray, ToList; an async one, ExecuteQueryAsync, too): enumerating it later fails, because its connection
    // is closed. A function that returns a task is refused (see Helpers)
    /// <summary>
    /// Opens a connection, runs the function with a context of the connection, closes the connection and returns
    /// the function's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// <para>
    /// A function that returns a task, as an async lambda does, is refused with an
    /// <c>InvalidOperationException</c>: the connection would be closed at its first <c>await</c>. Use
    /// <c>UsingConnectionAsync</c>.
    /// </para>
    /// </remarks>
    public TResult UsingConnection<TResult>(Func<ConnectionContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var conn = CreateConnection();
        conn.Open();
        var result = func(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
        Helpers.ThrowIfAsyncResult(result, nameof(UsingConnection), "the connection would be closed");
        return result;
    }

    /// <summary>
    /// Opens a connection, runs the action with a context of the connection and closes the connection when the
    /// action's task is done.
    /// </summary>
    public Task UsingConnectionAsync(Func<ConnectionContext, Task> action)
    {
        return UsingConnectionAsync(action, CancellationToken.None);
    }

    // The token cancels the opening of the connection. The action is not given the token: it passes its own to
    // the commands it runs. A token that is cancelled already ends the call here: a driver that takes the
    // connection from its pool does not look at the token
    /// <summary>
    /// Opens a connection, runs the action with a context of the connection and closes the connection when the
    /// action's task is done.
    /// </summary>
    /// <remarks>
    /// The token cancels opening the connection; the delegate does not get it, so pass it to the commands inside.
    /// </remarks>
    public Task UsingConnectionAsync(Func<ConnectionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : UsingConnectionCoreAsync(action, cancellationToken);
    }

    // as UsingConnection<TResult>, with the token of the overload above
    /// <summary>
    /// Opens a connection, runs the function with a context of the connection, closes the connection when the
    /// function's task is done and returns its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The token cancels opening the connection; the delegate does not get it, so pass it to the commands inside.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// </remarks>
    public Task<TResult> UsingConnectionAsync<TResult>(Func<ConnectionContext, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled<TResult>(cancellationToken) : UsingConnectionCoreAsync(func, cancellationToken);
    }

    // For FastSql's own commands: a cancelled token is left to the driver, which reports it with its own
    // exception when the connection is opened or the command is executed
    internal async Task UsingConnectionCoreAsync(Func<ConnectionContext, Task> action, CancellationToken cancellationToken)
    {
        var conn = CreateConnection();
        await using var connectionScope = conn.ConfigureAwait(false);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
    }

    internal async Task<TResult> UsingConnectionCoreAsync<TResult>(Func<ConnectionContext, Task<TResult>> func, CancellationToken cancellationToken)
    {
        var conn = CreateConnection();
        await using var connectionScope = conn.ConfigureAwait(false);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await func(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
    }

    #endregion

    #region UsingTransaction

    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits and
    /// closes the connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// For async work use <c>UsingTransactionAsync</c>. An async lambda written in the call binds to the overload
    /// with a result, which refuses it; an async method passed as this <c>Action</c> would have the transaction
    /// committed at its first <c>await</c>.
    /// </para>
    /// </remarks>
    public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, action));
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits and
    /// closes the connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// For async work use <c>UsingTransactionAsync</c>. An async lambda written in the call binds to the overload
    /// with a result, which refuses it; an async method passed as this <c>Action</c> would have the transaction
    /// committed at its first <c>await</c>.
    /// </para>
    /// </remarks>
    public void UsingTransaction(Action<TransactionContext> action)
    {
        UsingTransaction(IsolationLevel.Unspecified, action);
    }

    // a connection is opened for the transaction: see ConnectionContext.UsingTransaction<TResult>
    /// <summary>
    /// Opens a connection, begins a transaction, runs the function with a context of the transaction, commits,
    /// closes the connection and returns the function's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// <para>
    /// A function that returns a task, as an async lambda does, is refused with an
    /// <c>InvalidOperationException</c>, after a rollback: the transaction would be committed at its first
    /// <c>await</c>. Use <c>UsingTransactionAsync</c>.
    /// </para>
    /// </remarks>
    public TResult UsingTransaction<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, func));
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the function with a context of the transaction, commits,
    /// closes the connection and returns the function's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// <para>
    /// A function that returns a task, as an async lambda does, is refused with an
    /// <c>InvalidOperationException</c>, after a rollback: the transaction would be committed at its first
    /// <c>await</c>. Use <c>UsingTransactionAsync</c>.
    /// </para>
    /// </remarks>
    public TResult UsingTransaction<TResult>(Func<TransactionContext, TResult> func)
    {
        return UsingTransaction(IsolationLevel.Unspecified, func);
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits when
    /// the action's task is done and closes the connection.
    /// </summary>
    /// <remarks>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </remarks>
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(isolationLevel, action, CancellationToken.None);
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits when
    /// the action's task is done and closes the connection.
    /// </summary>
    /// <remarks>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </remarks>
    public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, CancellationToken.None);
    }

    // the token cancels the opening of the connection, the beginning of the transaction and the commit
    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits when
    /// the action's task is done and closes the connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels opening the connection, beginning and committing the transaction; the delegate does not
    /// get it, so pass it to the commands inside.
    /// </para>
    /// </remarks>
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, action, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the action with a context of the transaction, commits when
    /// the action's task is done and closes the connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels opening the connection, beginning and committing the transaction; the delegate does not
    /// get it, so pass it to the commands inside.
    /// </para>
    /// </remarks>
    public Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, cancellationToken);
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the function with a context of the transaction, commits when
    /// the function's task is done, closes the connection and returns its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels opening the connection, beginning and committing the transaction; the delegate does not
    /// get it, so pass it to the commands inside.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// </remarks>
    public Task<TResult> UsingTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, func, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Opens a connection, begins a transaction, runs the function with a context of the transaction, commits when
    /// the function's task is done, closes the connection and returns its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels opening the connection, beginning and committing the transaction; the delegate does not
    /// get it, so pass it to the commands inside.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the connection is closed, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// </remarks>
    public Task<TResult> UsingTransactionAsync<TResult>(Func<TransactionContext, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, func, cancellationToken);
    }

    #endregion
}
