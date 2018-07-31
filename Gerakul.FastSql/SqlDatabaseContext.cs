using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Gerakul.FastSql
{
    public class SqlDatabaseContext : DatabaseContext
    {
        internal static readonly SqlDatabaseContext Instance = new SqlDatabaseContext();

        private SqlDatabaseContext()
            : base()
        {
        }

        public override QueryOptions DefaultQueryOptions { get; } = new SqlQueryOptions();

        public ExecutionOptions GetExecutionOptions(int? commandTimeoutSeconds = null, FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return new ExecutionOptions(new SqlQueryOptions(commandTimeoutSeconds), new ReadOptions(fieldsSelector, caseSensitive, fromTypeOption));
        }

        protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        {
            base.ApplyQueryOptions(cmd, queryOptions);
        }

        protected override string GetSqlParameterName(string name)
        {
            return name[0] == '@' ? name.Substring(1) : name;
        }

        protected override BulkOptions GetDafaultBulkOptions()
        {
            return new SqlBulkOptions();
        }

        protected override BulkCopy GetBulkCopy(DbConnection connection, DbTransaction transaction, BulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields)
        {
            return new MsSqlBulkCopy((SqlConnection)connection, (SqlTransaction)transaction, (SqlBulkOptions)bulkOptions, destinationTable, reader, fields);
        }

        protected override DbConnection GetConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }
    }
}
