using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // Checks the integration test infrastructure: containers, per-class databases, seed data, context kinds,
    // scratch tables and session counting. Concrete classes are in the provider folders.
    public abstract class SmokeTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected SmokeTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        [Fact]
        public void Connection_PointsToDatabaseOfTestClass()
        {
            using (var connection = Database.OpenConnection())
            {
                connection.Database.Should().Be(Database.DatabaseName);
            }
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void SeedData_IsPresent(ContextKind kind)
        {
            var companies = Database.Run(kind, x => x.CreateSimple($"select * from {Spec.Quote("Company")} order by {Spec.Quote("ID")}")
                .ExecuteQuery<Company>().ToArray());
            var employees = Database.Run(kind, x => x.CreateSimple($"select * from {Spec.Quote("Employee")} order by {Spec.Quote("ID")}")
                .ExecuteQuery<Employee>().ToArray());

            companies.Should().BeEquivalentTo(SeedData.Companies, o => o.WithStrictOrdering());
            employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task SeedData_IsPresentAsync(ContextKind kind)
        {
            var employees = await Database.RunAsync(kind, x => x.CreateSimple($"select * from {Spec.Quote("Employee")} order by {Spec.Quote("ID")}")
                .ExecuteQueryAsync<Employee>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Fact]
        public void TestProc_ReturnsEmployeesOfCompanyOlderThanAge()
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Run_KeepsChangesMadeInContext(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("ID")} int not null");

            Database.Run(kind, x => x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery());

            CountRows(table).Should().Be(1);
        }

        [Fact]
        public void CreateScratchTable_CreatesEmptyTablesWithUniqueNames()
        {
            var first = Database.CreateScratchTable($"{Spec.Quote("ID")} int not null");
            var second = Database.CreateScratchTable($"{Spec.Quote("ID")} int not null");

            first.Should().NotBe(second);
            CountRows(first).Should().Be(0);
            CountRows(second).Should().Be(0);
        }

        [Fact]
        public void SessionCounting_SeesOpenedAndClosedConnections()
        {
            var applicationName = "fastsql-" + Guid.NewGuid().ToString("N");
            var connectionString = Database.CreateSessionTrackedConnectionString(applicationName);

            using (var connection = Spec.CreateConnection(connectionString))
            {
                connection.Open();
                Database.WaitForSessionCount(applicationName, 1).Should().Be(1);
            }

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}")
                .ExecuteQueryFirstColumn<int>().Single());
        }
    }
}
