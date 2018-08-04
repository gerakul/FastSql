using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class DbContext
    {
        protected internal abstract CommandTextGenerator CommandTextGenerator { get; }
        public abstract QueryOptions DefaultQueryOptions { get; }

        public ExecutionOptions DefaultExecutionOptions { get; }
        public string ConnectionString { get; }

        public DbContext(string connectionString)
        {
            this.DefaultExecutionOptions = new ExecutionOptions(DefaultQueryOptions, new ReadOptions());
            this.ConnectionString = connectionString;
        }

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

        protected internal abstract string GetSqlParameterName(string name);

        // :::
        //protected internal abstract BulkOptions GetDafaultBulkOptions();

        //protected internal abstract BulkCopy GetBulkCopy(DbConnection connection, DbTransaction transaction, BulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields);

        protected internal abstract DbConnection GetConnection();

        protected internal abstract object GetDbNull(Type type);

        protected internal abstract string[] ParseCommandText(string commandText);
    }
}
