using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I8: the seeded TestProc through CreateProcedure and CreateProcedureSimple. On SQL Server it is a stored procedure,
    // on PostgreSQL a function that Npgsql 4 calls as "select * from TestProc("CompanyID" := ..., "Age" := ...)".
    // Provider-only scenarios are in the concrete classes in the provider folders.
    public abstract class StoredProcedureTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected StoredProcedureTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        // the exception type the driver throws for server errors
        protected abstract Type ServerExceptionType { get; }

        // stable part of the server message for TestProc called with an extra Extra argument
        protected abstract string ExtraArgumentMessage { get; }

        // the driver's error for a DbParameter added to a second command
        protected abstract Type ParameterOfAnotherCommandExceptionType { get; }

        protected abstract string ParameterOfAnotherCommandMessage { get; }

        // employees of company 1 older than 20
        protected static Employee[] ExpectedEmployees => SeedData.Employees.Where(x => x.CompanyID == 1 && x.Age > 20).ToArray();

        protected static string NewName(string prefix)
        {
            return prefix + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_TestProc_ReturnsMatchingEmployees(ContextKind kind)
        {
            var employees = Database.Run(kind, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 20 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_TestProc_ReturnsMatchingEmployeesAsync(ContextKind kind)
        {
            var employees = await Database.RunAsync(kind, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 20 })
                .ExecuteQueryAsync<Employee>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        // parameters are passed by name, so the member order does not matter
        [Fact]
        public void CreateProcedure_MembersInOtherOrder_ReturnsMatchingEmployees()
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new { Age = 20, CompanyID = 1 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Fact]
        public void CreateProcedure_WithQueryOptions_ReturnsMatchingEmployees()
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 20 }, new QueryOptions(30))
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        // every member becomes a parameter
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_ExtraMember_ThrowsServerError(ContextKind kind)
        {
            Action act = () => Database.Run(kind, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQuery<Employee>().ToArray());

            act.Should().Throw<DbException>().WithMessage(ExtraArgumentMessage)
                .Which.Should().BeOfType(ServerExceptionType);
        }

        [Fact]
        public async Task CreateProcedure_ExtraMember_ThrowsServerErrorAsync()
        {
            Func<Task> act = () => Database.RunAsync(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQueryAsync<Employee>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<DbException>()).WithMessage(ExtraArgumentMessage)
                .Which.Should().BeOfType(ServerExceptionType);
        }

        // only the listed parameters are passed; names match members with or without "@"
        // (names in another case than the procedure's parameters: see the concrete classes)
        [Theory]
        [InlineData("CompanyID", "Age")]
        [InlineData("@CompanyID", "@Age")]
        public void CreateProcedure_ParameterNames_PassesOnlyListedMembers(string companyIdName, string ageName)
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new[] { companyIdName, ageName },
                new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        // settings give the parameter names, so members can be named differently
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_Settings_UsesSettingNamesAndGetters(ContextKind kind)
        {
            var settings = new[]
            {
                new FieldSettings<Filter>("CompanyID", typeof(int), x => x.Company),
                new FieldSettings<Filter>("Age", typeof(int), x => x.OlderThan)
            };

            var employees = Database.Run(kind, x => x.CreateProcedure("TestProc", settings, new Filter() { Company = 1, OlderThan = 20 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Fact]
        public void CreateProcedure_ParameterNamesAndSettings_PassesOnlyListedSettings()
        {
            var settings = new List<FieldSettings<Filter>>()
            {
                new FieldSettings<Filter>("CompanyID", typeof(int), x => x.Company),
                new FieldSettings<Filter>("Age", typeof(int), x => x.OlderThan),
                new FieldSettings<Filter>("Extra", typeof(string), x => "not passed")
            };

            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new[] { "CompanyID", "Age" }, settings,
                new Filter() { Company = 1, OlderThan = 20 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        // CreateProcedure takes members from fields and properties by default (FromTypeOption.Both)
        [Fact]
        public void CreateProcedure_FromTypeOptionPublicField_IgnoresProperties()
        {
            var filter = new FieldFilter() { CompanyID = 1, Age = 20, Extra = 5 };

            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", filter, null, FromTypeOption.PublicField)
                .ExecuteQuery<Employee>().ToArray());
            Action withProperties = () => Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", filter)
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
            withProperties.Should().Throw<DbException>().WithMessage(ExtraArgumentMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedureSimple_ProviderParameters_ReturnsMatchingEmployees(ContextKind kind)
        {
            var employees = Database.Run(kind, x => x.CreateProcedureSimple("TestProc", Spec.CreateParameter("CompanyID", 1), Spec.CreateParameter("Age", 20))
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedureSimple_ProviderParameters_ReturnsMatchingEmployeesAsync(ContextKind kind)
        {
            var employees = await Database.RunAsync(kind, x => x.CreateProcedureSimple("TestProc", Spec.CreateParameter("CompanyID", 1), Spec.CreateParameter("Age", 20))
                .ExecuteQueryAsync<Employee>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Fact]
        public void CreateProcedureSimple_WithQueryOptions_ReturnsMatchingEmployees()
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedureSimple(new QueryOptions(30), "TestProc",
                Spec.CreateParameter("CompanyID", 1), Spec.CreateParameter("Age", 20))
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        // scoped contexts build the command once and run it again
        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q92")]
        public void CreateProcedureSimple_ExecutedTwiceOnScopedContext_RunsTwice(ContextKind kind)
        {
            var results = Database.Run(kind, x =>
            {
                var command = x.CreateProcedureSimple("TestProc", Spec.CreateParameter("CompanyID", 1), Spec.CreateParameter("Age", 20));
                return new[] { command.ExecuteQuery<Employee>().ToArray(), command.ExecuteQuery<Employee>().ToArray() };
            });

            results[0].Should().BeEquivalentTo(ExpectedEmployees);
            results[1].Should().BeEquivalentTo(ExpectedEmployees);
        }

        // connection string contexts build a new DbCommand for every execution and add the same DbParameter objects to it,
        // but a parameter still belongs to the previous command, so the second execution fails
        [Fact]
        [Trait("Quirk", "Q92")]
        public void CreateProcedureSimple_ExecutedTwiceOnConnectionStringContext_SecondExecutionThrows()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var command = context.CreateProcedureSimple("TestProc", Spec.CreateParameter("CompanyID", 1), Spec.CreateParameter("Age", 20));

            var first = command.ExecuteQuery<Employee>().ToArray();
            Action second = () => command.ExecuteQuery<Employee>().ToArray();

            first.Should().BeEquivalentTo(ExpectedEmployees);
            second.Should().Throw<Exception>().WithMessage(ParameterOfAnotherCommandMessage)
                .Which.Should().BeOfType(ParameterOfAnotherCommandExceptionType);
        }

        private class Filter
        {
            public int Company { get; set; }
            public int OlderThan { get; set; }
        }

        private class FieldFilter
        {
            public int CompanyID;
            public int Age;

            public int Extra { get; set; }
        }
    }
}
