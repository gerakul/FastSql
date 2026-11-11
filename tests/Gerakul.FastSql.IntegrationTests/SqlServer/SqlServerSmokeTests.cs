using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerSmokeTests : SmokeTests<SqlServerDatabase>
    {
        public SqlServerSmokeTests(SqlServerDatabase database)
            : base(database)
        {
        }
    }
}
