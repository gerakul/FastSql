using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlConnectionStringContext : ConnectionStringContext
    {
        public NpgsqlConnectionStringContext(ContextProvider contextProvider, string connectionString) 
            : base(contextProvider, connectionString)
        {
        }

        protected override DbConnection CreateConnection()
        {
            return new NpgsqlConnection(ConnectionString);
        }
    }
}
