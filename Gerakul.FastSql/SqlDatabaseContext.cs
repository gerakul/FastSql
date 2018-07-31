using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql
{
    public class SqlDatabaseContext : DatabaseContext
    {
        public override QueryOptions DefaultQueryOptions { get; } = new SqlQueryOptions();

        protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        {
            base.ApplyQueryOptions(cmd, queryOptions);
        }

        protected override string GetSqlParameterName(string name)
        {
            return name[0] == '@' ? name.Substring(1) : name;
        }

        public ExecutionOptions GetExecutionOptions(int? commandTimeoutSeconds = null, FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new ExecutionOptions(new SqlQueryOptions(commandTimeoutSeconds), new ReadOptions(fieldsSelector, caseSensitive, fromTypeOption));
        }
    }
}
