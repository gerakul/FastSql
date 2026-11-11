using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class ConnectionContext : ScopedContext
    {
        public DbConnection Connection { get; }

        protected internal ConnectionContext(ContextProvider contextProvider, DbConnection connection,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, queryOptions, bulkOptions, readOptions)
        {
            this.Connection = connection;
        }

        public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
        {
            using (DbTransaction tran = Connection.BeginTransaction(isolationLevel))
            {
                try
                {
                    action(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions));
                }
                catch
                {
                    Rollback(tran);
                    throw;
                }

                tran.Commit();
            }
        }

        public void UsingTransaction(Action<TransactionContext> action)
        {
            UsingTransaction(IsolationLevel.Unspecified, action);
        }

        public async Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
        {
            DbTransaction tran = await Connection.BeginTransactionAsync(isolationLevel).ConfigureAwait(false);
            await using var transactionScope = tran.ConfigureAwait(false);

            try
            {
                await action(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
            }
            catch
            {
                await RollbackAsync(tran).ConfigureAwait(false);
                throw;
            }

            await tran.CommitAsync().ConfigureAwait(false);
        }

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

        public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
        {
            return UsingTransactionAsync(IsolationLevel.Unspecified, action);
        }
    }
}
