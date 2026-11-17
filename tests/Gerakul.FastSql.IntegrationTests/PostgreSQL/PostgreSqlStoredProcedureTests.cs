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
    // Npgsql 7+ runs CommandType.StoredProcedure as "CALL name("param" => $1, ...)": the name is used verbatim (unquoted,
    // so it folds to lower case) and the parameter names are quoted. That calls procedures only; functions (like the
    // seeded TestProc) are rejected by the server. Npgsql 4 ran "select * from name(...)", which called functions only.
    [Trait("Provider", "PostgreSQL")]
    [Trait("UpgradeSensitive", "Npgsql 7+ calls procedures with CALL instead of functions with SELECT")]
    public class PostgreSqlStoredProcedureTests : StoredProcedureTests<PostgreSqlDatabase>
    {
        public PostgreSqlStoredProcedureTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string ExtraArgumentMessage => "42883: procedure testproc(CompanyID => integer, Age => integer, Extra => integer) does not exist*";

        protected override Type ParameterOfAnotherCommandExceptionType => typeof(InvalidOperationException);

        protected override string ParameterOfAnotherCommandMessage => "The parameter already belongs to a collection";

        // the quoted parameter names must match the routine's parameter names exactly
        [Fact]
        public void CreateProcedure_ParameterNamesInOtherCase_ThrowsServerError()
        {
            Action act = () => Database.Run(ContextKind.ConnectionString, x => x.CreateProcedure("TestProc", new[] { "companyid", "age" },
                new { CompanyID = 1, Age = 20, Extra = 5 })
                .ExecuteQuery<Employee>().ToArray());

            act.Should().Throw<PostgresException>().WithMessage("42883: procedure testproc(companyid => integer, age => integer) does not exist*");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_FunctionReturningTable_ThrowsServerError(ContextKind kind)
        {
            var name = CreateTableFunction();

            Action act = () => Database.Run(kind, x => x.CreateProcedure(name, new { CompanyID = 1, MinAge = 20 })
                .ExecuteQuery<NameAndAge>().ToArray());

            act.Should().Throw<PostgresException>().WithMessage($"42883: procedure {name}(CompanyID => integer, MinAge => integer) does not exist*");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_FunctionReturningTable_ThrowsServerErrorAsync(ContextKind kind)
        {
            var name = CreateTableFunction();

            Func<Task> act = () => Database.RunAsync(kind, x => x.CreateProcedure(name, new { CompanyID = 1, MinAge = 20 })
                .ExecuteQueryAsync<NameAndAge>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<PostgresException>().WithMessage($"42883: procedure {name}(CompanyID => integer, MinAge => integer) does not exist*");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_VoidFunction_ThrowsServerError(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            Action act = () => Database.Run(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQuery());

            act.Should().Throw<PostgresException>().WithMessage($"42809: {name}(Value => integer) is not a procedure*");
            ReadValues(table).Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateProcedure_VoidFunction_ThrowsServerErrorAsync(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            Func<Task> act = () => Database.RunAsync(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<PostgresException>().WithMessage($"42809: {name}(Value => integer) is not a procedure*");
            ReadValues(table).Should().BeEmpty();
        }

        [Fact]
        public void CreateProcedureSimple_VoidFunction_ThrowsServerError()
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateVoidFunction(table);

            Action act = () => Database.Run(ContextKind.ConnectionString, x => x.CreateProcedureSimple(name, Spec.CreateParameter("Value", 6)).ExecuteNonQuery());

            act.Should().Throw<PostgresException>().WithMessage($"42809: {name}(Value => integer) is not a procedure*");
            ReadValues(table).Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_Procedure_RunsProcedure(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateInsertingProcedure(table);

            var mapped = Database.Run(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteNonQuery());
            var simple = Database.Run(kind, x => x.CreateProcedureSimple(name, Spec.CreateParameter("Value", 6)).ExecuteNonQuery());

            mapped.Should().Be(-1);
            simple.Should().Be(-1);
            ReadValues(table).Should().Equal(5, 6);
        }

        [Fact]
        public async Task CreateProcedure_Procedure_RunsProcedureAsync()
        {
            var table = Database.CreateScratchTable($"{Spec.Quote("Value")} int not null");
            var name = CreateInsertingProcedure(table);

            var affected = await Database.RunAsync(ContextKind.ConnectionString, x => x.CreateProcedure(name, new { Value = 5 })
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(-1);
            ReadValues(table).Should().Equal(5);
        }

        // CALL returns one row with the values of the INOUT and OUT parameters
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateProcedure_ProcedureWithInoutParameter_ReturnsItsValue(ContextKind kind)
        {
            var name = NewName("proc_");
            Database.Execute($"create procedure {name}(\"Value\" int, inout \"Doubled\" int default null) language plpgsql as $$ "
                + "begin \"Doubled\" := \"Value\" * 2; end $$");

            var values = Database.Run(kind, x => x.CreateProcedure(name, new { Value = 5 }).ExecuteQueryFirstColumn<int>().ToArray());

            values.Should().Equal(10);
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
