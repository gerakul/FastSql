using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class ConnectionContext : ScopedContext
    {
        protected DbConnection connection;

        public ConnectionContext(ContextProvider contextProvider, DbConnection connection) 
            : base(contextProvider)
        {
            this.connection = connection;
        }

        public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
        {
            DbTransaction tran = null;
            try
            {
                tran = connection.BeginTransaction(isolationLevel);
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
                tran = connection.BeginTransaction(isolationLevel);
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
