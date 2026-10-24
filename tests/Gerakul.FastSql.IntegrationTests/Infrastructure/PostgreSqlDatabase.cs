using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Npgsql;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    public class PostgreSqlDatabase : DatabaseFixture
    {
        public PostgreSqlDatabase(PostgreSqlContainerFixture container)
            : base(container)
        {
        }

        protected override string SeedResourceName => "Seeds.PostgreSql.sql";

        protected override string CountSessionsCommandText => "select count(*) from pg_stat_activity where application_name = @applicationName";

        protected override string WithDatabase(string connectionString, string database)
        {
            return new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ConnectionString;
        }

        protected override string WithSessionTracking(string connectionString, string applicationName)
        {
            return new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false, ApplicationName = applicationName }.ConnectionString;
        }

        // TestProc is a function (Seeds/PostgreSql.sql). Since Npgsql 7, CommandType.StoredProcedure runs
        // "CALL name(param => $1, ...)", which calls procedures only, so the server rejects the call before anything else
        // happens; its message shows the arguments that were passed. Npgsql 4 ran "select * from name(...)", which
        // called the function.
        public override void CallTestProc(Action call, params string[] arguments)
        {
            call.Should().ThrowExactly<PostgresException>().WithMessage(TestProcIsNotAProcedureMessage(arguments));
        }

        public override async Task CallTestProcAsync(Func<Task> call, params string[] arguments)
        {
            await call.Should().ThrowExactlyAsync<PostgresException>().WithMessage(TestProcIsNotAProcedureMessage(arguments));
        }

        private static string TestProcIsNotAProcedureMessage(string[] arguments)
        {
            return $"42809: testproc({string.Join(", ", arguments.Select(x => x + " => integer"))}) is not a procedure*";
        }
    }
}
