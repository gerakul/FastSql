using System;
using System.Data.SqlClient;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerMergeTests : MergeTests<SqlServerDatabase>
    {
        public SqlServerMergeTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);

        protected override string EmptyUpdateClauseMessage => "Incorrect syntax near the keyword 'when'.";

        // MERGE matches rows by the on clause and needs no unique constraint (unlike PostgreSQL on conflict)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_TableWithoutUniqueConstraint_InsertsAndUpdates(ContextKind kind)
        {
            var table = Database.CreateScratchTable($"{Q("ID")} int not null, {Q("GroupID")} int not null, {Q("Name")} varchar(50) null, {Q("Age")} int null");

            Database.Run(kind, x =>
            {
                x.Merge(table, new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 }, "ID");
                x.Merge(table, new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 31 }, "ID");
            });

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 31 } });
        }

        // an existing row is updated with just the given members, even when a not null column is left out
        // (PostgreSQL rejects the same call)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q91")]
        public void Merge_SomeMembersOfExistingRow_UpdatesOnlyThoseColumns(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 });

            Database.Run(kind, x => x.Merge(table, new { ID = 1, Name = "Alicia" }, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 30 } });
        }
    }
}
