using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlMergeTests : MergeTests<PostgreSqlDatabase>
    {
        public PostgreSqlMergeTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int generated always as identity";

        protected override Type ServerExceptionType => typeof(PostgresException);

        protected override string EmptyUpdateClauseMessage => "42601: syntax error at end of input";

        // "on conflict (keys)" needs a unique constraint or index on exactly the key columns
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_TableWithoutUniqueConstraint_ThrowsServerError(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Q("ID")} int not null, {Q("GroupID")} int not null, {Q("Name")} varchar(50) null, {Q("Age")} int null");

            ShouldThrowServerError(() => Database.Run(kind, x => x.Merge(table, new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 }, "ID")),
                "42P10: there is no unique or exclusion constraint matching the ON CONFLICT specification");

            ReadPersons(table).Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Merge_TableWithoutUniqueConstraint_ThrowsServerErrorAsync(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Q("ID")} int not null, {Q("GroupID")} int not null, {Q("Name")} varchar(50) null, {Q("Age")} int null");

            await ShouldThrowServerErrorAsync(() => Database.RunAsync(kind, x => x.MergeAsync(table, new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 }, "ID")),
                "42P10: there is no unique or exclusion constraint matching the ON CONFLICT specification");

            ReadPersons(table).Should().BeEmpty();
        }

        // "insert ... on conflict" checks the not null constraints of the proposed row before it looks for the conflict,
        // so leaving out a not null column fails even though the row exists and would only be updated (SQL Server updates it)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q91")]
        public void Merge_SomeMembersOfExistingRow_ThrowsNotNullViolation(ContextKind kind)
        {
            var table = CreatePersonTable();
            var existing = new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 };
            InsertPersons(table, existing);

            ShouldThrowServerError(() => Database.Run(kind, x => x.Merge(table, new { ID = 1, Name = "Alicia" }, "ID")),
                $"23502: null value in column \"GroupID\" of relation \"{table}\" violates not-null constraint");

            ReadPersons(table).Should().BeEquivalentTo(new[] { existing });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q91")]
        public async Task Merge_SomeMembersOfExistingRow_ThrowsNotNullViolationAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            var existing = new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 };
            InsertPersons(table, existing);

            await ShouldThrowServerErrorAsync(() => Database.RunAsync(kind, x => x.MergeAsync(table, new { ID = 1, Name = "Alicia" }, "ID")),
                $"23502: null value in column \"GroupID\" of relation \"{table}\" violates not-null constraint");

            ReadPersons(table).Should().BeEquivalentTo(new[] { existing });
        }

        // leaving out only nullable columns works: the existing row is updated with just the given members
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q91")]
        public void Merge_SomeMembersOfExistingRowCoveringNotNullColumns_UpdatesOnlyThoseColumns(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 });

            Database.Run(kind, x => x.Merge(table, new { ID = 1, GroupID = 1, Name = "Alicia" }, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 30 } });
        }

        // the conflict target must match a unique constraint exactly: one column of a composite key is not enough
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_KeysCoveringPartOfCompositeKey_ThrowsServerError(ContextKind kind)
        {
            var table = CreateMembershipTable();

            ShouldThrowServerError(() => Database.Run(kind, x => x.Merge(table, new Membership() { GroupID = 1, Code = "A", Name = "first" }, "GroupID")),
                "42P10: there is no unique or exclusion constraint matching the ON CONFLICT specification");
        }
    }
}
