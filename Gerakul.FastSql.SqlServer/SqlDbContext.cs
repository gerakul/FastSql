using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlDbContext : DbContext
    {
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
    }
}
