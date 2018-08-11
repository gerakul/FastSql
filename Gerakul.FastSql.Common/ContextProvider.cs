using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class ContextProvider
    {
        public abstract QueryOptions DefaultQueryOptions { get; }
        public abstract BulkOptions DefaultBulkOptions { get; }

        public abstract CommandTextGenerator CommandTextGenerator { get; }
        public CommandCompilator CommandCompilator { get; }

        public ContextProvider()
        {
            this.CommandCompilator = new CommandCompilator(this);
        }

        public abstract ConnectionStringContext CreateConnectionStringContext(string connectionString);
        public abstract ConnectionContext CreateConnectionContext(DbConnection connection);
        public abstract TransactionContext CreateTransactionContext(DbTransaction transaction);

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
    }
}
