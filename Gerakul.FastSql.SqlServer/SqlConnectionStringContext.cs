using Gerakul.FastSql.Common;
using System.Data.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlConnectionStringContext : ConnectionStringContext
    {
        public SqlConnectionStringContext(ContextProvider contextProvider, string connectionString) 
            : base(contextProvider, connectionString)
        {
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
