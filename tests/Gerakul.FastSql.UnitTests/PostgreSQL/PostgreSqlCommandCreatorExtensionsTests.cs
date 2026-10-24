using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class PostgreSqlCommandCreatorExtensionsTests : CommandCreatorExtensionsTests<PostgreSqlSpec>
    {
        protected override string ClosedConnectionMessage => "Connection is not open";

        protected override string ClosedConnectionAsyncMessage => "Connection is not open";
    }
}
