using System;
using System.Data.SqlClient;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerWrappedCommandQueryTests : WrappedCommandQueryTests<SqlServerDatabase>
    {
        public SqlServerWrappedCommandQueryTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string MultipleResultSetsCommandText =>
            "select ID, Name from Company order by ID; select Name from Employee order by ID";

        protected override Type ServerExceptionType => typeof(SqlException);

        protected override string NoSuchTableMessage => "Invalid object name 'no_such_table'.";
    }
}
