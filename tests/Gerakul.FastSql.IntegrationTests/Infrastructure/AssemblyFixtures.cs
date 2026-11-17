using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]
[assembly: AssemblyFixture(typeof(PostgreSqlContainerFixture))]
