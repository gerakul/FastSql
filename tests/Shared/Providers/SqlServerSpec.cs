using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Microsoft.Data.SqlClient;

namespace Gerakul.FastSql.Tests.Shared
{
    public class SqlServerSpec : ProviderSpec
    {
        public override ProviderKind Kind => ProviderKind.SqlServer;

        public override ContextProvider DefaultProvider => SqlContextProvider.DefaultInstance;

        public override ContextProvider CreateProvider()
        {
            return new SqlContextProvider();
        }

        public override DbConnection CreateConnection(string connectionString = null)
        {
            return connectionString == null ? new SqlConnection() : new SqlConnection(connectionString);
        }

        public override string Quote(string identifier)
        {
            return $"[{identifier}]";
        }
    }
}
