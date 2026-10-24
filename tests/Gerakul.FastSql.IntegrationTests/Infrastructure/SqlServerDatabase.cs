using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    public class SqlServerDatabase : DatabaseFixture
    {
        private static readonly Regex batchSeparator = new Regex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public SqlServerDatabase(SqlServerContainerFixture container)
            : base(container)
        {
        }

        protected override string SeedResourceName => "Seeds.SqlServer.sql";

        protected override string CountSessionsCommandText => "select count(*) from sys.dm_exec_sessions where program_name = @applicationName";

        protected override string WithDatabase(string connectionString, string database)
        {
            return new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString;
        }

        protected override string WithSessionTracking(string connectionString, string applicationName)
        {
            return new SqlConnectionStringBuilder(connectionString) { Pooling = false, ApplicationName = applicationName }.ConnectionString;
        }

        protected override IEnumerable<string> SplitScript(string script)
        {
            return batchSeparator.Split(script).Where(x => !string.IsNullOrWhiteSpace(x));
        }
    }
}
