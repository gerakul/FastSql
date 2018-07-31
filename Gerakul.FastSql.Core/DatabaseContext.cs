using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Core
{
    public abstract class DatabaseContext
    {
        public abstract QueryOptions DefaultQueryOptions { get; }

        public ExecutionOptions DefaultExecutionOptions { get; }

        public DatabaseContext()
        {
            this.DefaultExecutionOptions = new ExecutionOptions(DefaultQueryOptions, new ReadOptions());
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

        protected internal abstract BulkOptions GetDafaultBulkOptions();

        protected internal abstract BulkCopy GetBulkCopy(DbConnection connection, DbTransaction transaction, BulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields);

        protected internal abstract DbConnection GetConnection(string connectionString);
    }
}
