using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class ContextProvider
    {
        public abstract QueryOptions QueryOptions { get; }
        public abstract BulkOptions BulkOptions { get; }
        public virtual ReadOptions ReadOptions { get; } = new ReadOptions();

        public abstract CommandTextGenerator CommandTextGenerator { get; }
        public CommandCompilator CommandCompilator { get; }

        public ContextProvider()
        {
            this.CommandCompilator = new CommandCompilator(this);
        }

        public ConnectionStringContext CreateContext(string connectionString)
        {
            return GetConnectionStringContext(connectionString, QueryOptions, BulkOptions, ReadOptions);
        }

        public ConnectionContext CreateContext(DbConnection connection)
        {
            return GetConnectionContext(connection, QueryOptions, BulkOptions, ReadOptions);
        }

        public TransactionContext CreateContext(DbTransaction transaction)
        {
            return GetTransactionContext(transaction, QueryOptions, BulkOptions, ReadOptions);
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
