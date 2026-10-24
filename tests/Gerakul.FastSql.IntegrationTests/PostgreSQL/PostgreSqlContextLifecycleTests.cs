using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.PostgreSQL;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlContextLifecycleTests : ContextLifecycleTests<PostgreSqlDatabase>
    {
        public PostgreSqlContextLifecycleTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string CurrentIsolationLevelCommandText => "show transaction_isolation";

        protected override TransactionContext CreateTransactionContextByConstructor(DbTransaction transaction)
        {
            return new NpgsqlTransactionContext(NpgsqlContextProvider.DefaultInstance, (NpgsqlTransaction)transaction);
        }

        protected override string ClosedConnectionMessage(string operation)
        {
            return "Connection is not open";
        }
    }
}
