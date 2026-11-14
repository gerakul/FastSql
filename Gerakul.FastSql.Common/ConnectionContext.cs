using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

public abstract class ConnectionContext : ScopedContext
{
    public DbConnection Connection { get; }

    protected internal ConnectionContext(ContextProvider contextProvider, DbConnection connection,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.Connection = connection;
    }

    #region UsingTransaction

    public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        UsingTransaction(isolationLevel, x =>
        {
            action(x);
            return true;
        });
    }

    public void UsingTransaction(Action<TransactionContext> action)
    {
        UsingTransaction(IsolationLevel.Unspecified, action);
    }

    // The transaction is committed when the function returns and rolled back when it throws. A query that is
    // returned must have been read (ToArray, ToList): after the commit its command cannot be used anymore
    public TResult UsingTransaction<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        using DbTransaction tran = Connection.BeginTransaction(isolationLevel);
        TResult result;

        try
        {
            result = func(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions));
        }
        catch
        {
            Rollback(tran);
            throw;
        }

        tran.Commit();
        return result;
    }

    public TResult UsingTransaction<TResult>(Func<TransactionContext, TResult> func)
    {
        return UsingTransaction(IsolationLevel.Unspecified, func);
    }

    #endregion

    #region UsingTransactionAsync

    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(isolationLevel, action, CancellationToken.None);
    }

    public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, CancellationToken.None);
    }

    // The token cancels the beginning of the transaction and the commit: when it is cancelled before the commit,
    // the transaction is rolled back. The action is not given the token: it passes its own to the commands it runs
    public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : UsingTransactionCoreAsync(isolationLevel, async x =>
        {
            await action(x).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    public Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken)
    {
        return UsingTransactionAsync(IsolationLevel.Unspecified, action, cancellationToken);
    }

    // as UsingTransaction<TResult>, with the token of the overloads above
    public Task<TResult> UsingTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        return cancellationToken.IsCancellationRequested ? Task.FromCanceled<TResult>(cancellationToken) : UsingTransactionCoreAsync(isolationLevel, func, cancellationToken);
    }

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
