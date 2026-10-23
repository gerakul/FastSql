using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerReadmeScenarioTests : ReadmeScenarioTests<SqlServerDatabase>
    {
        public SqlServerReadmeScenarioTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string EmployeeColumnsDefinition =>
            "[ID] int identity(1,1) not null primary key, [CompanyID] int not null, [Name] nvarchar(50) not null, "
            + "[Phone] nvarchar(50) null, [Age] int null, [StartWorking] datetime null";

        protected override string CompanyColumnsDefinition =>
            "[ID] int not null primary key, [Name] nvarchar(50) not null, [DateOfFoundation] date not null";

        protected override DbParameter CreateReadmeParameter(string name, object value)
        {
            return new SqlParameter(name, value);
        }

        protected override string N(string identifier)
        {
            return identifier;
        }

        protected override void ShouldThrowConnectionBusy(Action import, string sourceCommandText)
        {
            import.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("There is already an open DataReader associated with this Command which must be closed first.");
        }

        #region Bulk copy and data import

        // SqlBulkCopy ignores the source values of the identity column (no KeepIdentity), so the new rows get new IDs
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void BulkCopy_NewEmployees_InsertsRowsWithGeneratedIds(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);
            var before = DateTime.UtcNow;

            BulkCopyNewEmployees(kind, employeeTable);

            var after = DateTime.UtcNow;
            var added = ReadEmployees(employeeTable).Skip(5).ToArray();
            added.Select(x => new { x.ID, x.CompanyID, x.Name, x.Phone, x.Age }).Should().BeEquivalentTo(new[]
            {
                new { ID = 6, CompanyID = 1, Name = "New Employee1", Phone = (string)null, Age = (int?)23 },
                new { ID = 7, CompanyID = 1, Name = "New Employee2", Phone = (string)null, Age = (int?)null },
                new { ID = 8, CompanyID = 2, Name = "New Employee1", Phone = (string)null, Age = (int?)null }
            }, o => o.WithStrictOrdering());
            // the datetime column keeps the wall clock of DateTime.UtcNow (rounded to 1/300 s), so it is compared with UTC wall clock values
            added[0].StartWorking.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
            added[1].StartWorking.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
            added[2].StartWorking.Should().BeNull();
        }

        [Fact]
        public void DataImport_QueryIntoItsSourceTableOnConnectionStringContext_CopiesRowsWithNewIds()
        {
            var employeeTable = CreateEmployeeTable(true);

            ImportQueryIntoItsSourceTable(ContextKind.ConnectionString, employeeTable);

            var expected = SeedData.Employees.Concat(SeedData.Employees.Select(x => WithId(x, x.ID + 5)));
            ReadEmployees(employeeTable).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        #endregion

        #region Extensibility

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateSelectTop10Employees_CustomExtension_ReturnsEmployees(ContextKind kind)
        {
            // using custom extensions of ICommandCreator
            var data1 = Database.Run(kind, context => context.CreateSelectTop10Employees().ExecuteQuery<Employee>().ToArray());

            data1.Should().BeEquivalentTo(SeedData.Employees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void GetTable_CustomExtension_ReturnsAllRows(ContextKind kind)
        {
            var data2 = Database.Run(kind, context => context.GetTable<Employee>("Employee"));

            data2.Should().BeEquivalentTo(SeedData.Employees);
        }

        #endregion

        private static Employee WithId(Employee employee, int id)
        {
            return new Employee()
            {
                ID = id,
                CompanyID = employee.CompanyID,
                Name = employee.Name,
                Phone = employee.Phone,
                Age = employee.Age,
                StartWorking = employee.StartWorking
            };
        }
    }
}
