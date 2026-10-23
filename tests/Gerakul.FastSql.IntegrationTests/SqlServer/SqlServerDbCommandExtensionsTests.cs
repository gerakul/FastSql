using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerDbCommandExtensionsTests : DbCommandExtensionsTests<SqlServerDatabase>
    {
        public SqlServerDbCommandExtensionsTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override Type OpenReaderExceptionType => typeof(InvalidOperationException);

        protected override string OpenReaderMessage(string openCommandText)
        {
            return "There is already an open DataReader associated with this Command which must be closed first.";
        }
    }
}
