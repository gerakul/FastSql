using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class ConnectionStringContext : DbContext
    {
        public string ConnectionString { get; }

        protected internal ConnectionStringContext(ContextProvider contextProvider, string connectionString,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, queryOptions, bulkOptions, readOptions)
        {
            this.ConnectionString = connectionString;
        }

        protected internal abstract DbConnection CreateConnection();

        public void UsingConnection(Action<ConnectionContext> action)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
            }
        }

        public async Task UsingConnectionAsync(Func<ConnectionContext, Task> action)
        {
            using (var conn = CreateConnection())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
            }
        }

        public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
        {
            UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, action));
        }

        public void UsingTransaction(Action<TransactionContext> action)
        {
            UsingTransaction(IsolationLevel.Unspecified, action);
        }

        public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
        {
            return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, action));
        }

        public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
        {
            return UsingTransactionAsync(IsolationLevel.Unspecified, action);
        }
    }
}
