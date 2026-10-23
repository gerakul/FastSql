using System;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlUpdateTests : UpdateTests<PostgreSqlDatabase>
    {
        public PostgreSqlUpdateTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int generated always as identity";

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string EmptySetClauseMessage => "42601: syntax error at or near \"where\"";

        // Npgsql leaves the unknown @ID in the text, and PostgreSQL reads it as the prefix operator @ applied to the column id
        protected override string UnmappedKeyParameterMessage => "42703: column \"id\" does not exist";
    }
}
