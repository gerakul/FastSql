using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlInsertWithOutputTests : InsertWithOutputTests<PostgreSqlDatabase>
    {
        public PostgreSqlInsertWithOutputTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int generated always as identity";

        protected override Type ServerExceptionType => typeof(PostgresException);
    }
}
