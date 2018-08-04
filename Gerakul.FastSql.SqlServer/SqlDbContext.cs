using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlDbContext : DbContext
    {
        private readonly SqlCommandTextGenerator commandTextGenerator = new SqlCommandTextGenerator();

        protected override CommandTextGenerator CommandTextGenerator { get; } = new SqlCommandTextGenerator();
        public override QueryOptions DefaultQueryOptions { get; } = new SqlQueryOptions();

        public SqlDbContext(string connectionString) 
            : base(connectionString)
        {
        }

        //protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        //{
        //    base.ApplyQueryOptions(cmd, queryOptions);
        //}

        protected override DbConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        protected override object GetDbNull(Type type)
        {
            if (type.Equals(typeof(byte[])))
            {
                return System.Data.SqlTypes.SqlBytes.Null;
            }

            return DBNull.Value;
        }

        protected override string GetSqlParameterName(string name)
        {
            return name[0] == '@' ? name.Substring(1) : name;
        }

        private static Regex regName = new Regex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline);
        protected override string[] ParseCommandText(string commandText)
        {
            return regName.Matches(commandText).Cast<Match>()
              .Select(x => "@" + x.Groups["Name"].ToString().ToLowerInvariant())
              .Distinct().ToArray();
        }
    }
}
