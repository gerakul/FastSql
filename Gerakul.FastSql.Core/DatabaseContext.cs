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
    }
}
