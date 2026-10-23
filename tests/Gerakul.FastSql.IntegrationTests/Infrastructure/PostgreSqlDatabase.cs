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
    }
}
