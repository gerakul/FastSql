using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlExecutionSemanticsTests : ExecutionSemanticsTests<PostgreSqlDatabase>
    {
        public PostgreSqlExecutionSemanticsTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string SleepCommandText(int seconds)
        {
            return $"select pg_sleep({seconds})";
        }

        protected override Type CancellationExceptionType(string operation)
        {
            return typeof(OperationCanceledException);
        }

        // Npgsql enforces the timeout as a read timeout (the inner exception was an IOException in Npgsql 4)
        protected override void AssertCommandTimeout(Exception exception)
        {
            var npgsqlException = exception.Should().BeOfType<NpgsqlException>().Which;
            npgsqlException.Message.Should().Be("Exception while reading from stream");
            npgsqlException.InnerException.Should().BeOfType<TimeoutException>().Which.Message.Should().Be("Timeout during reading attempt");
        }

        // Npgsql 4 did not apply CommandTimeout to async execution (Q71), so the whole sleep ran
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "Npgsql 10 applies CommandTimeout to async execution (Npgsql 4 did not)")]
        public async Task ExecuteNonQueryAsync_CommandTimeoutShorterThanServerSleep_ThrowsNpgsqlExceptionAsync(ContextKind kind)
        {
            var stopwatch = Stopwatch.StartNew();

            var exception = await CatchAsync(() => Database.RunAsync(kind, x => x.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteNonQueryAsync(Token)));

            AssertCommandTimeout(exception);
            // the timeout cut the 3 second sleep short (1 second timeout; the bound leaves room for a slow machine)
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2.9));
        }

        [Fact]
        [Trait("UpgradeSensitive", "Npgsql 10 applies CommandTimeout to async execution (Npgsql 4 did not)")]
        public async Task QueryMethodsAsync_CommandTimeoutShorterThanServerSleep_ThrowNpgsqlExceptionAsync()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            var scalar = await CatchAsync(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteScalarAsync(Token));
            var rows = await CatchAsync(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteQueryAsync(Token).ToListCompat(Token));

            AssertCommandTimeout(scalar);
            AssertCommandTimeout(rows);
        }
    }
}
