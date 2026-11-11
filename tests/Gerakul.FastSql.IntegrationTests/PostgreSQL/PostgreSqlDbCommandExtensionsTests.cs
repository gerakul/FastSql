using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlDbCommandExtensionsTests : DbCommandExtensionsTests<PostgreSqlDatabase>
    {
        public PostgreSqlDbCommandExtensionsTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override Type OpenReaderExceptionType => typeof(NpgsqlOperationInProgressException);

        protected override string OpenReaderMessage(string openCommandText)
        {
            return "A command is already in progress: " + openCommandText;
        }
    }
}
