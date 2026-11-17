using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerInsertWithOutputTests : InsertWithOutputTests<SqlServerDatabase>
    {
        public SqlServerInsertWithOutputTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);
    }
}
