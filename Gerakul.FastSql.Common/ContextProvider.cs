using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class ContextProvider
    {
        public abstract QueryOptions DefaultQueryOptions { get; }
        public abstract BulkOptions DefaultBulkOptions { get; }
        public virtual ReadOptions DefaultReadOptions { get; } = new ReadOptions();

        public abstract CommandTextGenerator CommandTextGenerator { get; }
        public CommandCompilator CommandCompilator { get; }

        public ContextProvider()
        {
            this.CommandCompilator = new CommandCompilator(this);
        }

        public ConnectionStringContext CreateConnectionStringContext(string connectionString)
        {
            return GetConnectionStringContext(connectionString, DefaultQueryOptions, DefaultBulkOptions, DefaultReadOptions);
        }

        public ConnectionContext CreateConnectionContext(DbConnection connection)
        {
            return GetConnectionContext(connection, DefaultQueryOptions, DefaultBulkOptions, DefaultReadOptions);
        }

        public TransactionContext CreateTransactionContext(DbTransaction transaction)
        {
            return GetTransactionContext(transaction, DefaultQueryOptions, DefaultBulkOptions, DefaultReadOptions);
        }

        protected internal abstract ConnectionStringContext GetConnectionStringContext(string connectionString, 
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);
        protected internal abstract ConnectionContext GetConnectionContext(DbConnection connection, 
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);
        protected internal abstract TransactionContext GetTransactionContext(DbTransaction transaction, 
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);

        protected internal virtual void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        {
            if (queryOptions == null)
            {
                return;
            }

            if (queryOptions.CommandTimeoutSeconds.HasValue)
            {
                cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
            }
        }

        protected internal abstract DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value);

        protected internal abstract string GetSqlParameterName(string name);

        protected internal abstract object GetDbNull(Type type);

        protected internal abstract string[] ParamsFromCommandText(string commandText);

        protected internal abstract string[] ParamsFromSettings<T>(IEnumerable<FieldSettings<T>> settings);

        protected internal abstract BulkCopy GetBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields);

        protected internal abstract QueryOptions GetTyped(QueryOptions options, out bool needClone);

        protected internal abstract BulkOptions GetTyped(BulkOptions options, out bool needClone);
    }
}
