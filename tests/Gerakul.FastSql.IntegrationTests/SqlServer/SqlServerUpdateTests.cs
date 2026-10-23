using System;
using System.Data.SqlClient;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerUpdateTests : UpdateTests<SqlServerDatabase>
    {
        public SqlServerUpdateTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);

        protected override string EmptySetClauseMessage => "Incorrect syntax near the keyword 'where'.";

        protected override string UnmappedKeyParameterMessage => "Must declare the scalar variable \"@ID\".";
    }
}
