using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    // Npgsql 4 runs CommandType.StoredProcedure as "select * from name("param" := $1, ...)": the name is used verbatim
    // (unquoted, so it folds to lower case) and the parameter names are quoted. That calls functions, not procedures.
    [Trait("Provider", "PostgreSQL")]
    [Trait("UpgradeSensitive", "Npgsql 7+ calls procedures with CALL instead of functions with SELECT")]
    public class PostgreSqlStoredProcedureTests : StoredProcedureTests<PostgreSqlDatabase>
    {
        public PostgreSqlStoredProcedureTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string ExtraArgumentMessage => "42883: function testproc(CompanyID => integer, Age => integer, Extra => integer) does not exist";

        protected override Type ParameterOfAnotherCommandExceptionType => typeof(InvalidOperationException);

        protected override string ParameterOfAnotherCommandMessage => "The parameter already belongs to a collection";

        // the quoted parameter names must match the function's parameter names exactly
        [Fact]
        public void CreateProcedure_ParameterNamesInOtherCase_ThrowsServerError()
        {
            Action act = () => Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new[] { "companyid", "age" },
                new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQuery<Employee>().ToArray());

            act.Should().Throw<PostgresException>().WithMessage("42883: function testproc(companyid => integer, age => integer) does not exist");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_FunctionReturningTable_ReturnsRows(ContextKind kind)
        {
            var name = CreateTableFunction();

            var rows = Database.Run(kind, x => x.CreateProcedure(name, new { CompanyID = 1, MinAge = 20 })
                .ExecuteQuery<NameAndAge>().ToArray());

            rows.Should().BeEquivalentTo(new[]
            {
                new NameAndAge() { Name = "John Smith", Age = 46 },
                new NameAndAge() { Name = "Engelbert Kaufmann", Age = 23 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_FunctionReturningTable_ReturnsRowsAsync(ContextKind kind)
        {
            var name = CreateTableFunction();

            var rows = await Database.RunAsync(kind, x => x.CreateProcedure(name, new { CompanyID = 1, MinAge = 20 })
                .ExecuteQueryAsync<NameAndAge>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            rows.Should().BeEquivalentTo(new[]
            {
                new NameAndAge() { Name = "John Smith", Age = 46 },
                new NameAndAge() { Name = "Engelbert Kaufmann", Age = 23 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_VoidFunction_ExecuteNonQueryRunsFunction(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            var affected = Database.Run(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQuery());

            affected.Should().Be(-1);
            ReadValues(table).Should().Equal(5);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_VoidFunction_ExecuteNonQueryRunsFunctionAsync(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            var affected = await Database.RunAsync(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(-1);
            ReadValues(table).Should().Equal(5);
        }

        [Fact]
        public void CreateProcedureSimple_VoidFunction_ExecuteNonQueryRunsFunction()
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            Database.Run(ContextKind.ConnectionString, x => x.CreateProcedureSimple(name, Spec.CreateParameter("Value", 6)).ExecuteNonQuery());

            ReadValues(table).Should().Equal(6);
        }

        // a PostgreSQL procedure cannot be called with SELECT, so neither CreateProcedure nor CreateProcedureSimple reach it
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_Procedure_ThrowsServerError(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateInsertingProcedure(table);
            var expectedMessage = $"42809: {name}(Value => integer) is a procedure";

            Action mapped = () => Database.Run(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQuery());
            Action simple = () => Database.Run(kind, x => x.CreateProcedureSimple(name, Spec.CreateParameter("Value", 5)).ExecuteNonQuery());

            mapped.Should().Throw<PostgresException>().WithMessage(expectedMessage);
            simple.Should().Throw<PostgresException>().WithMessage(expectedMessage);
            ReadValues(table).Should().BeEmpty();
        }

        [Fact]
        public async Task CreateProcedure_Procedure_ThrowsServerErrorAsync()
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateInsertingProcedure(table);

            Func<Task> act = () => Database.RunAsync(ContextKind.ConnectionString, x => x.CreateProcedure(name, new { Value = 5 })
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<PostgresException>()).WithMessage($"42809: {name}(Value => integer) is a procedure");
            ReadValues(table).Should().BeEmpty();
        }

        // lower-case unquoted names, quoted parameter names (Npgsql quotes them in the call)
        private string CreateTableFunction()
        {
            var name = NewName("fn_");
            Database.Execute($"create function {name}(\"MinAge\" int, \"CompanyID\" int) returns table(\"Name\" varchar, \"Age\" int) language sql as $$ "
                + "select e.\"Name\", e.\"Age\" from \"Employee\" e where e.\"CompanyID\" = $2 and e.\"Age\" > $1 order by e.\"ID\" $$");
            return name;
        }

        private string CreateVoidFunction(string table)
        {
            var name = NewName("fn_");
            Database.Execute($"create function {name}(\"Value\" int) returns void language sql as $$ insert into {table} (\"Value\") values ($1) $$");
            return name;
        }

        private string CreateInsertingProcedure(string table)
        {
            var name = NewName("proc_");
            Database.Execute($"create procedure {name}(\"Value\" int) language sql as $$ insert into {table} (\"Value\") values ($1) $$");
            return name;
        }

        private int[] ReadValues(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select {Spec.Quote("Value")} from {table}")
                .ExecuteQueryFirstColumn<int>().ToArray());
        }

        private class NameAndAge
        {
            public string Name { get; set; }
            public int? Age { get; set; }
        }
    }
}
