using System;
using System.Diagnostics;
using System.IO;
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

        // Npgsql 4 enforces the timeout as a socket read timeout
        protected override void AssertCommandTimeout(Exception exception)
        {
            var npgsqlException = exception.Should().BeOfType<NpgsqlException>().Which;
            npgsqlException.Message.Should().Be("Exception while reading from stream");
            npgsqlException.InnerException.Should().BeOfType<IOException>();
        }

        protected override Type OpenReaderExceptionType => typeof(NpgsqlOperationInProgressException);

        protected override string OpenReaderMessage(string openCommandText)
        {
            return "A command is already in progress: " + openCommandText;
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q71")]
        [Trait("UpgradeSensitive", "Npgsql 4 does not apply CommandTimeout to async execution")]
        public async Task ExecuteNonQueryAsync_CommandTimeoutShorterThanServerSleep_RunsToCompletionAsync(ContextKind kind)
        {
            var stopwatch = Stopwatch.StartNew();

            var exception = await CatchAsync(() => Database.RunAsync(kind, x => x.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteNonQueryAsync(Token)));

            exception.Should().BeNull();
            // the whole sleep ran (a lower bound only, so a slow machine cannot break it)
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(2.5));
        }

        [Fact]
        [Trait("Quirk", "Q71")]
        [Trait("UpgradeSensitive", "Npgsql 4 does not apply CommandTimeout to async execution")]
        public async Task QueryMethodsAsync_CommandTimeoutShorterThanServerSleep_RunToCompletionAsync()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            var scalar = await context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteScalarAsync(Token);
            var rows = await context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteQueryAsync(Token).ToListCompat(Token);

            // pg_sleep returns void, which Npgsql 4 reads as DBNull
            scalar.Should().Be(DBNull.Value);
            rows.Should().ContainSingle().Which.Should().Equal(DBNull.Value);
        }
    }
}
