using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlWrappedCommandQueryTests : WrappedCommandQueryTests<PostgreSqlDatabase>
    {
        public PostgreSqlWrappedCommandQueryTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        // Npgsql splits the text into statements and returns one result set per statement
        protected override string MultipleResultSetsCommandText =>
            "select \"ID\", \"Name\" from \"Company\" order by \"ID\"; select \"Name\" from \"Employee\" order by \"ID\"";

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string NoSuchTableMessage => "42P01: relation \"no_such_table\" does not exist";
    }
}
