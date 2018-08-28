using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Npgsql;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlContextProvider : ContextProvider
    {
        public static NpgsqlContextProvider DefaultInstance { get; } = new NpgsqlContextProvider();

        private NpgsqlContextProvider()
        {
        }

        public override CommandTextGenerator CommandTextGenerator { get; } = new NpgsqlCommandTextGenerator();
        public override QueryOptions DefaultQueryOptions { get; } = new NpgsqlQueryOptions();
        public override BulkOptions DefaultBulkOptions { get; } = new NpgsqlBulkOptions();

        protected override ConnectionStringContext GetConnectionStringContext(string connectionString, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlConnectionStringContext(this, connectionString, queryOptions, bulkOptions, readOptions);
        }

        protected override ConnectionContext GetConnectionContext(DbConnection connection, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlConnectionContext(this, (NpgsqlConnection)connection, queryOptions, bulkOptions, readOptions);
        }

        protected override TransactionContext GetTransactionContext(DbTransaction transaction, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlTransactionContext(this, (NpgsqlTransaction)transaction, queryOptions, bulkOptions, readOptions);
        }

        //protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        //{
        //    base.ApplyQueryOptions(cmd, queryOptions);
        //}

        protected override DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value)
        {
            return ((NpgsqlCommand)cmd).Parameters.AddWithValue(paramName, value);
        }

        protected override object GetDbNull(Type type)
        {
            return DBNull.Value;
        }

        protected override string GetSqlParameterName(string name)
        {
            return name[0] == '@' ? name.Substring(1) : name;
        }

        private static Regex regName = new Regex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline);
        protected override string[] ParamsFromCommandText(string commandText)
        {
            return regName.Matches(commandText).Cast<Match>()
              .Select(x => "@" + x.Groups["Name"].ToString().ToLowerInvariant())
              .Distinct().ToArray();
        }

        protected override string[] ParamsFromSettings<T>(IEnumerable<FieldSettings<T>> settings)
        {
            return settings.Select(x => "@" + x.Name.ToLowerInvariant()).ToArray();
        }

        protected override BulkCopy GetBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return new NpgsqlBulkCopy(context, reader, destinationTable, (NpgsqlBulkOptions)bulkOptions, fields);
        }

        protected override QueryOptions GetTyped(QueryOptions options, out bool needClone)
        {
            needClone = false;
            return new NpgsqlQueryOptions(options);
        }

        protected override BulkOptions GetTyped(BulkOptions options, out bool needClone)
        {
            needClone = false;
            return new NpgsqlBulkOptions(options);
        }
    }
}
