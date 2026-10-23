using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Npgsql;

namespace Gerakul.FastSql.Tests.Shared
{
    public class PostgreSqlSpec : ProviderSpec
    {
        public override ProviderKind Kind => ProviderKind.PostgreSql;

        public override ContextProvider DefaultProvider => NpgsqlContextProvider.DefaultInstance;

        public override ContextProvider CreateProvider()
        {
            return new NpgsqlContextProvider();
        }

        public override DbConnection CreateConnection(string connectionString = null)
        {
            return connectionString == null ? new NpgsqlConnection() : new NpgsqlConnection(connectionString);
        }

        public override string Quote(string identifier)
        {
            return $"\"{identifier}\"";
        }
    }
}
