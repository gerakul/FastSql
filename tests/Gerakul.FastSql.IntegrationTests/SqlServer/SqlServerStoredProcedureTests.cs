using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerStoredProcedureTests : StoredProcedureTests<SqlServerDatabase>
    {
        public SqlServerStoredProcedureTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override Type ServerExceptionType => typeof(SqlException);

        protected override string ExtraArgumentMessage => "Procedure or function TestProc has too many arguments specified.";

        protected override Type ParameterOfAnotherCommandExceptionType => typeof(ArgumentException);

        protected override string ParameterOfAnotherCommandMessage => "The SqlParameter is already contained by another SqlParameterCollection.";

        // parameter names are matched by the server, case-insensitively (default collation)
        [Fact]
        public void CreateProcedure_ParameterNamesInOtherCase_ReturnsMatchingEmployees()
        {
            var employees = Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new[] { "companyid", "age" },
                new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(ExpectedEmployees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedureSimple_OutputAndReturnValueParameters_HaveValuesAfterExecution(ContextKind kind)
        {
            var name = CreateDoublingProcedure();
            var parameters = DoublingParameters(5);

            var affected = Database.Run(kind, x => x.CreateProcedureSimple(name, parameters).ExecuteNonQuery());

            affected.Should().Be(-1);
            parameters[1].Value.Should().Be(10);
            parameters[2].Value.Should().Be(6);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedureSimple_OutputAndReturnValueParameters_HaveValuesAfterExecutionAsync(ContextKind kind)
        {
            var name = CreateDoublingProcedure();
            var parameters = DoublingParameters(5);

            var affected = await Database.RunAsync(kind, x => x.CreateProcedureSimple(name, parameters).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(-1);
            parameters[1].Value.Should().Be(10);
            parameters[2].Value.Should().Be(6);
        }

        // a member whose value is a DbParameter is added as it is, under the parameter's own name
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_DbParameterMembers_AreAddedAsTheyAre(ContextKind kind)
        {
            var name = CreateDoublingProcedure();
            var parameters = DoublingParameters(0);

            Database.Run(kind, x => x.CreateProcedure(name, new { Value = 7, Output = parameters[1], Result = parameters[2] }).ExecuteNonQuery());

            parameters[1].Value.Should().Be(14);
            parameters[2].Value.Should().Be(8);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_DbParameterMembers_AreAddedAsTheyAreAsync(ContextKind kind)
        {
            var name = CreateDoublingProcedure();
            var parameters = DoublingParameters(0);

            await Database.RunAsync(kind, x => x.CreateProcedure(name, new { Value = 7, Output = parameters[1], Result = parameters[2] })
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            parameters[1].Value.Should().Be(14);
            parameters[2].Value.Should().Be(8);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_SeveralResultSets_UseReaderReachesAll(ContextKind kind)
        {
            var name = CreateTwoResultSetsProcedure();
            IdAndName[] companies = null;
            IdAndName[] employees = null;
            var hasSecond = false;
            var hasThird = true;

            Database.Run(kind, x => x.CreateProcedure(name, new { CompanyID = 2 }).UseReader(reader =>
            {
                companies = reader.ReadAll<IdAndName>().ToArray();
                hasSecond = reader.NextResult();
                employees = reader.ReadAll<IdAndName>().ToArray();
                hasThird = reader.NextResult();
            }));

            companies.Should().BeEquivalentTo(new[] { new IdAndName() { ID = 2, Name = "Oldest Co" } });
            hasSecond.Should().BeTrue();
            employees.Should().BeEquivalentTo(new[]
            {
                new IdAndName() { ID = 4, Name = "Ivan Antonov" },
                new IdAndName() { ID = 5, Name = "Kate Donahue" }
            }, o => o.WithStrictOrdering());
            hasThird.Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_SeveralResultSets_UseReaderReachesAllAsync(ContextKind kind)
        {
            var name = CreateTwoResultSetsProcedure();
            var cancellationToken = TestContext.Current.CancellationToken;
            List<IdAndName> companies = null;
            List<IdAndName> employees = null;
            var hasSecond = false;
            var hasThird = true;

            await Database.RunAsync(kind, x => x.CreateProcedure(name, new { CompanyID = 2 }).UseReaderAsync(cancellationToken, async reader =>
            {
                companies = await reader.ReadAllAsync<IdAndName>().ToListCompat(cancellationToken);
                hasSecond = await reader.NextResultAsync(cancellationToken);
                employees = await reader.ReadAllAsync<IdAndName>().ToListCompat(cancellationToken);
                hasThird = await reader.NextResultAsync(cancellationToken);
            }));

            companies.Should().BeEquivalentTo(new[] { new IdAndName() { ID = 2, Name = "Oldest Co" } });
            hasSecond.Should().BeTrue();
            employees.Should().BeEquivalentTo(new[]
            {
                new IdAndName() { ID = 4, Name = "Ivan Antonov" },
                new IdAndName() { ID = 5, Name = "Kate Donahue" }
            }, o => o.WithStrictOrdering());
            hasThird.Should().BeFalse();
        }

        // creates a procedure with a unique name in the class database; CREATE PROCEDURE is a batch of its own
        private string CreateStoredProcedure(string parametersAndBody)
        {
            var name = NewName("proc_");
            Database.Execute($"create procedure {name} {parametersAndBody}");
            return name;
        }

        private string CreateDoublingProcedure()
        {
            return CreateStoredProcedure("@Value int, @Doubled int output as begin set nocount on; set @Doubled = @Value * 2; return @Value + 1; end");
        }

        private string CreateTwoResultSetsProcedure()
        {
            return CreateStoredProcedure("@CompanyID int as begin set nocount on; "
                + "select ID, Name from Company where ID = @CompanyID; "
                + "select ID, Name from Employee where CompanyID = @CompanyID order by ID; end");
        }

        // input @Value, output @Doubled, return value
        private DbParameter[] DoublingParameters(int value)
        {
            var doubled = Spec.CreateParameter("@Doubled", null, ParameterDirection.Output);
            doubled.DbType = DbType.Int32;
            var returnValue = Spec.CreateParameter("@ReturnValue", null, ParameterDirection.ReturnValue);
            returnValue.DbType = DbType.Int32;

            return new[] { Spec.CreateParameter("@Value", value), doubled, returnValue };
        }

        private class IdAndName
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }
    }
}
