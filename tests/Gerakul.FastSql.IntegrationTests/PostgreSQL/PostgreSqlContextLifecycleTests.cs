using System;
using System.Data.Common;
using System.Threading.Tasks;
using AwesomeAssertions;
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

        // PostgreSQL checks a deferred constraint at commit, and the failed commit ends the transaction, so a rollback
        // afterwards would fail with "This NpgsqlTransaction has completed"
        [Fact]
        [Trait("Fixed", "Q123")]
        public void UsingTransaction_DeferredConstraintFailsAtCommit_ThrowsPostgresException()
        {
            var child = CreateChildWithDeferredForeignKey();
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            Action act = () => context.UsingTransaction(x => x.CreateSimple($"insert into {child} (\"ParentID\") values (999)").ExecuteNonQuery());

            act.Should().Throw<PostgresException>().Which.SqlState.Should().Be("23503");
        }

        [Fact]
        [Trait("Fixed", "Q123")]
        public async Task UsingTransactionAsync_DeferredConstraintFailsAtCommit_ThrowsPostgresExceptionAsync()
        {
            var child = CreateChildWithDeferredForeignKey();

            using (var connection = await Database.OpenConnectionAsync())
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                Func<Task> act = () => context.UsingTransactionAsync(x => x.CreateSimple($"insert into {child} (\"ParentID\") values (999)")
                    .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

                (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23503");
            }
        }

        private string CreateChildWithDeferredForeignKey()
        {
            var parent = Database.CreateScratchTable("\"ID\" int primary key");
            return Database.CreateScratchTable($"\"ParentID\" int references {parent} (\"ID\") deferrable initially deferred");
        }
    }
}
