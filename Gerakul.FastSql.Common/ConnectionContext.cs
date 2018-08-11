using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class ConnectionContext : ScopedContext
    {
        public DbConnection Connection { get; }

        public ConnectionContext(ContextProvider contextProvider, DbConnection connection) 
            : base(contextProvider)
        {
            this.Connection = connection;
        }

        public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
        {
            DbTransaction tran = null;
            try
            {
                tran = Connection.BeginTransaction(isolationLevel);
                action(ContextProvider.CreateTransactionContext(tran));
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
                await action(ContextProvider.CreateTransactionContext(tran)).ConfigureAwait(false);
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
