using System;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlReadmeScenarioTests : ReadmeScenarioTests<PostgreSqlDatabase>
    {
        public PostgreSqlReadmeScenarioTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string EmployeeColumnsDefinition =>
            "\"ID\" serial primary key, \"CompanyID\" int not null, \"Name\" varchar(50) not null, "
            + "\"Phone\" varchar(50) null, \"Age\" int null, \"StartWorking\" timestamp null";

        protected override string CompanyColumnsDefinition =>
            "\"ID\" int not null primary key, \"Name\" varchar(50) not null, \"DateOfFoundation\" date not null";

        protected override DbParameter CreateReadmeParameter(string name, object value)
        {
            return new NpgsqlParameter(name, value);
        }

        protected override string N(string identifier)
        {
            return Spec.Quote(identifier);
        }

        protected override void AssertBulkCopyResult(Action bulkCopy, string table)
        {
            bulkCopy.Should().Throw<PostgresException>();
            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        protected override void AssertImportIntoSourceTableResult(ContextKind kind, Action import, string table)
        {
            import.Should().Throw<Exception>();
            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        protected override void AssertImportFromProcedureResult(ContextKind kind, Action import, string table)
        {
            if (kind == ContextKind.ConnectionString)
            {
                import();

                ReadEmployees(table).Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
            }
            else
            {
                import.Should().Throw<Exception>();
                CountRows(table).Should().Be(0);
            }
        }

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
            // the table name needs quotes on PostgreSQL (Q19)
            var data2 = Database.Run(kind, context => context.GetTable<Employee>("\"Employee\""));

            data2.Should().BeEquivalentTo(SeedData.Employees);
        }

        #endregion
    }
}
