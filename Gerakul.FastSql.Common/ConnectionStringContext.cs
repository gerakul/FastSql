using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

public abstract class ConnectionStringContext : DbContext
{
    public string ConnectionString { get; }

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

    protected internal abstract DbConnection CreateConnection();

    #region UsingConnection

    public void UsingConnection(Action<ConnectionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var conn = CreateConnection();
        conn.Open();
        action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
    }

    // The connection is closed when the function returns. A query that is returned must have been read
    // (ToArray, ToList): enumerating it later fails, because its connection is closed
    public TResult UsingConnection<TResult>(Func<ConnectionContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var conn = CreateConnection();
        conn.Open();
        return func(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
    }

    public Task UsingConnectionAsync(Func<ConnectionContext, Task> action)
    {
        return UsingConnectionAsync(action, CancellationToken.None);
    }

    // The token cancels the opening of the connection. The action is not given the token: it passes its own to
    // the commands it runs. A token that is cancelled already ends the call here: a driver that takes the
    // connection from its pool does not look at the token
    public Task UsingConnectionAsync(Func<ConnectionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : UsingConnectionCoreAsync(action, cancellationToken);
    }

    // as UsingConnection<TResult>, with the token of the overload above
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

    public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, action));
    }

    public void UsingTransaction(Action<TransactionContext> action)
    {
        UsingTransaction(IsolationLevel.Unspecified, action);
    }

    // a connection is opened for the transaction: see ConnectionContext.UsingTransaction<TResult>
    public TResult UsingTransaction<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, func));
    }

    public TResult UsingTransaction<TResult>(Func<TransactionContext, TResult> func)
    {
        return UsingTransaction(IsolationLevel.Unspecified, func);
    }

    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(isolationLevel, action, CancellationToken.None);
    }

    public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, CancellationToken.None);
    }

    // the token cancels the opening of the connection, the beginning of the transaction and the commit
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, action, cancellationToken), cancellationToken);
    }

    public Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, cancellationToken);
    }

    public Task<TResult> UsingTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, func, cancellationToken), cancellationToken);
    }

    public Task<TResult> UsingTransactionAsync<TResult>(Func<TransactionContext, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, func, cancellationToken);
    }

    #endregion
}
