using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlServerCommandCreatorExtensionsTests : CommandCreatorExtensionsTests<SqlServerSpec>
    {
        protected override string ClosedConnectionMessage =>
            "ExecuteNonQuery requires an open and available Connection. The connection's current state is closed.";

        protected override string ClosedConnectionAsyncMessage =>
            "BeginExecuteNonQuery requires an open and available Connection. The connection's current state is closed.";
    }
}
