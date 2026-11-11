using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerBulkCopyTests : BulkCopyTests<SqlServerDatabase>
    {
        public SqlServerBulkCopyTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null primary key";

        protected override string TextColumnType => "nvarchar(50)";

        protected override string DateTimeColumnType => "datetime2";
    }
}
