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
            DbTransaction tran = null;
            try
            {
                tran = Connection.BeginTransaction(isolationLevel);
                action(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions));
                tran.Commit();
            }
            catch
            {
                if (tran != null)
                {
                    tran.Rollback();
                }

                throw;
            }
        }

        public void UsingTransaction(Action<TransactionContext> action)
        {
            UsingTransaction(IsolationLevel.Unspecified, action);
        }

        public async Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
        {
            DbTransaction tran = null;
            try
            {
                tran = Connection.BeginTransaction(isolationLevel);
                await action(ContextProvider.GetTransactionContext(tran, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
                tran.Commit();
            }
            catch
            {
                if (tran != null)
                {
                    tran.Rollback();
                }

                throw;
            }
        }

        public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
        {
            return UsingTransactionAsync(IsolationLevel.Unspecified, action);
        }
    }
}
