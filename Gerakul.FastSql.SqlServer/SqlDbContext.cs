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
        public SqlDbContext(ContextProvider contextProvider, string connectionString) 
            : base(contextProvider, connectionString)
        {
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
