using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlSmokeTests : SmokeTests<PostgreSqlDatabase>
    {
        public PostgreSqlSmokeTests(PostgreSqlDatabase database)
            : base(database)
        {
        }
    }
}
