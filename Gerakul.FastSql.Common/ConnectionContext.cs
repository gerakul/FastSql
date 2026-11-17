using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A context over an open connection. Its commands run on that connection; the context does not close it.
/// </summary>
public abstract class ConnectionContext : ScopedContext
{
    /// <summary>
    /// The connection of the context.
    /// </summary>
    public DbConnection Connection { get; }

    /// <summary>
    /// Creates a context over the connection with copies of the given options.
    /// </summary>
    protected internal ConnectionContext(ContextProvider contextProvider, DbConnection connection,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.Connection = connection;
    }

    #region UsingTransaction

    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits.
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

        UsingTransaction(isolationLevel, x =>
        {
            action(x);
            return true;
        });
    }

    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits.
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

    // The transaction is committed when the function returns and rolled back when it throws. A query that is
    // returned must have been read (ToArray, ToList; an async one, ExecuteQueryAsync, too): after the commit its
    // command cannot be used anymore. A function that returns a task is refused and rolled back (see Helpers)
    /// <summary>
    /// Begins a transaction on the connection, runs the function with a context of the transaction, commits and
    /// returns the function's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the transaction has ended, and
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

        using DbTransaction tran = Connection.BeginTransaction(isolationLevel);
        TResult result;

        try
        {
            result = func(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions));
            Helpers.ThrowIfAsyncResult(result, nameof(UsingTransaction), "the transaction would be committed");
        }
        catch
        {
            Rollback(tran);
            throw;
        }

        tran.Commit();
        return result;
    }

    /// <summary>
    /// Begins a transaction on the connection, runs the function with a context of the transaction, commits and
    /// returns the function's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the transaction has ended, and
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

    #endregion

    #region UsingTransactionAsync

    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits when
    /// the action's task is done.
    /// </summary>
    /// <remarks>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </remarks>
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(isolationLevel, action, CancellationToken.None);
    }

    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits when
    /// the action's task is done.
    /// </summary>
    /// <remarks>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </remarks>
    public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, CancellationToken.None);
    }

    // The token cancels the beginning of the transaction and the commit: when it is cancelled before the commit,
    // the transaction is rolled back. The action is not given the token: it passes its own to the commands it runs
    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits when
    /// the action's task is done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels beginning and committing the transaction; the delegate does not get it, so pass it to the
    /// commands inside.
    /// </para>
    /// </remarks>
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : UsingTransactionCoreAsync(isolationLevel, async x =>
        {
            await action(x).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Begins a transaction on the connection, runs the action with a context of the transaction and commits when
    /// the action's task is done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels beginning and committing the transaction; the delegate does not get it, so pass it to the
    /// commands inside.
    /// </para>
    /// </remarks>
    public Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, cancellationToken);
    }

    // as UsingTransaction<TResult>, with the token of the overloads above
    /// <summary>
    /// Begins a transaction on the connection, runs the function with a context of the transaction, commits when
    /// the function's task is done and returns its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels beginning and committing the transaction; the delegate does not get it, so pass it to the
    /// commands inside.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the transaction has ended, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// </remarks>
    public Task<TResult> UsingTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled<TResult>(cancellationToken) : UsingTransactionCoreAsync(isolationLevel, func, cancellationToken);
    }

    /// <summary>
    /// Begins a transaction on the connection, runs the function with a context of the transaction, commits when
    /// the function's task is done and returns its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the delegate throws, the transaction is rolled back and the exception goes to the caller.
    /// </para>
    /// <para>
    /// The token cancels beginning and committing the transaction; the delegate does not get it, so pass it to the
    /// commands inside.
    /// </para>
    /// <para>
    /// Return what the caller needs, not a query: a query returned from the function (<c>ExecuteQuery</c>,
    /// <c>ExecuteQueryAsync</c> and the like) is read when it is enumerated, after the transaction has ended, and
    /// fails then. Return <c>ToArray()</c> or <c>ToList()</c> of it.
    /// </para>
    /// </remarks>
    public Task<TResult> UsingTransactionAsync<TResult>(Func<TransactionContext, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, func, cancellationToken);
    }

    private async Task<TResult> UsingTransactionCoreAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func,
        CancellationToken cancellationToken)
    {
        DbTransaction tran = await Connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        await using var transactionScope = tran.ConfigureAwait(false);
        TResult result;

        try
        {
            result = await func(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
        }
        catch
        {
            await RollbackAsync(tran).ConfigureAwait(false);
            throw;
        }

        // A commit that is cancelled or fails is not followed by a rollback: disposing the transaction rolls back.
        // The token is checked here, because not every driver looks at it before it commits
        cancellationToken.ThrowIfCancellationRequested();
        await tran.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    #endregion

    // the action's exception is what the caller needs: a rollback that fails too (broken connection, open reader)
    // must not replace it, and disposing the transaction rolls back whatever is still open
    private static void Rollback(DbTransaction tran)
    {
        try
        {
            tran.Rollback();
        }
        catch
        {
        }
    }

    private static async Task RollbackAsync(DbTransaction tran)
    {
        try
        {
            await tran.RollbackAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
