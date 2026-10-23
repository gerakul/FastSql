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

        protected override void ShouldThrowConnectionBusy(Action import, string sourceCommandText)
        {
            import.Should().ThrowExactly<NpgsqlOperationInProgressException>()
                .WithMessage($"A command is already in progress: {sourceCommandText}");
        }

        #region Bulk copy and data import

        // binary COPY writes the ID member (0 for every new employee), so the second row violates the primary key
        // and nothing is copied
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q111")]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public void BulkCopy_NewEmployees_ThrowsDuplicateKeyOnIdZero(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);

            Action act = () => BulkCopyNewEmployees(kind, employeeTable);

            act.Should().ThrowExactly<PostgresException>()
                .WithMessage($"23505: duplicate key value violates unique constraint \"{employeeTable}_pkey\"");
            ReadEmployees(employeeTable).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        // the copied rows keep their IDs, which are already in the table
        [Fact]
        [Trait("Quirk", "Q111")]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public void DataImport_QueryIntoItsSourceTableOnConnectionStringContext_ThrowsDuplicateKey()
        {
            var employeeTable = CreateEmployeeTable(true);

            Action act = () => ImportQueryIntoItsSourceTable(ContextKind.ConnectionString, employeeTable);

            act.Should().ThrowExactly<PostgresException>()
                .WithMessage($"23505: duplicate key value violates unique constraint \"{employeeTable}_pkey\"");
            ReadEmployees(employeeTable).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
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
            // the table name needs quotes on PostgreSQL (Q19)
            var data2 = Database.Run(kind, context => context.GetTable<Employee>("\"Employee\""));

            data2.Should().BeEquivalentTo(SeedData.Employees);
        }

        #endregion
    }
}
