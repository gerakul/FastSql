using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerExecutionSemanticsTests : ExecutionSemanticsTests<SqlServerDatabase>
    {
        public SqlServerExecutionSemanticsTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string SleepCommandText(int seconds)
        {
            return $"waitfor delay '00:00:{seconds:00}'";
        }

        protected override Type CancellationExceptionType(string operation)
        {
            return typeof(TaskCanceledException);
        }

        protected override void AssertCommandTimeout(Exception exception)
        {
            var sqlException = exception.Should().BeOfType<SqlException>().Which;
            sqlException.Number.Should().Be(-2);
            sqlException.Message.Should().Be("Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.");
        }

        protected override Type OpenReaderExceptionType => typeof(InvalidOperationException);

        protected override string OpenReaderMessage(string openCommandText)
        {
            return "There is already an open DataReader associated with this Command which must be closed first.";
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception for a command timeout")]
        public async Task ExecuteNonQueryAsync_CommandTimeoutShorterThanServerSleep_ThrowsSqlExceptionAsync(ContextKind kind)
        {
            var exception = await CatchAsync(() => Database.RunAsync(kind, x => x.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteNonQueryAsync(Token)));

            AssertCommandTimeout(exception);
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver exception for a command timeout")]
        public async Task QueryMethodsAsync_CommandTimeoutShorterThanServerSleep_ThrowSqlExceptionAsync()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            AssertCommandTimeout(await CatchAsync(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteScalarAsync(Token)));
            AssertCommandTimeout(await CatchAsync(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteQueryAsync(Token).ToListCompat(Token)));
        }
    }
}
