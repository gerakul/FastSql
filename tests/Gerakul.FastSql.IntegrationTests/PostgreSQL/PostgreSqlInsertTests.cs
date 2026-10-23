using System;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlInsertTests : InsertTests<PostgreSqlDatabase>
    {
        public PostgreSqlInsertTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int generated always as identity";

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string DefaultSchema => "public";

        protected override string ExplicitIdentityValueMessage(string table)
        {
            return "428C9: cannot insert a non-DEFAULT value into column \"ID\"";
        }

        // the generator quotes column names, so PascalCase members do not find unquoted (lower-case) columns
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q19")]
        public void Insert_LowerCaseColumnsAndPascalCaseMembers_ThrowsServerError(ContextKind kind)
        {
            var table = Database.CreateScratchTable("id int not null, name varchar(50) null");

            ShouldThrowServerError(() => Database.Run(kind, x => x.Insert(table, new { ID = 1, Name = "Alice" })),
                $"42703: column \"ID\" of relation \"{table}\" does not exist");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q19")]
        public void Insert_LowerCaseColumnsAndLowerCaseMembers_InsertsRow(ContextKind kind)
        {
            var table = Database.CreateScratchTable("id int not null, name varchar(50) null");

            Database.Run(kind, x => x.Insert(table, new { id = 1, name = "Alice" }));

            Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select id, name from {table}")
                .ExecuteQueryAnonymous(new { id = 0, name = "" }).ToArray())
                .Should().BeEquivalentTo(new[] { new { id = 1, name = "Alice" } });
        }

        // table names are not quoted by the generator: a mixed-case table name folds to lower case unless the caller quotes it
        [Fact]
        [Trait("Quirk", "Q19")]
        public void Insert_MixedCaseTableName_NeedsQuotesFromCaller()
        {
            var table = "Scratch_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Database.Execute($"create table {Q(table)} ({Q("ID")} int not null)");

            ShouldThrowServerError(() => Database.Run(ContextKind.ConnectionString, x => x.Insert(table, new { ID = 1 })),
                $"42P01: relation \"{table.ToLowerInvariant()}\" does not exist");

            Database.Run(ContextKind.ConnectionString, x => x.Insert(Q(table), new { ID = 2 }));

            Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select {Q("ID")} from {Q(table)}")
                .ExecuteQueryFirstColumn<int>().ToArray())
                .Should().Equal(2);
        }
    }
}
