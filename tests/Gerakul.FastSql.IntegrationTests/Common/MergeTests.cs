using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I7: Merge and MergeAsync against scratch tables with a primary key on the key fields
    // (SQL Server MERGE, PostgreSQL insert ... on conflict ... do update). Concrete classes are in the provider folders.
    public abstract class MergeTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected MergeTests(TDatabase database)
            : base(database)
        {
        }

        private static Person[] TwoPersons => new[]
        {
            new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
            new Person() { ID = 2, GroupID = 1, Name = "Bob", Age = 40 }
        };

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_MissingRow_InsertsRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = 50 };

            Database.Run(kind, x => x.Merge(table, merged, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { TwoPersons[0], TwoPersons[1], merged }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Merge_MissingRow_InsertsRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = 50 };

            await Database.RunAsync(kind, x => x.MergeAsync(table, merged, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { TwoPersons[0], TwoPersons[1], merged }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_ExistingRow_UpdatesRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 2, GroupID = 2, Name = "Robert", Age = 41 };

            Database.Run(kind, x => x.Merge(table, merged, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { TwoPersons[0], merged }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Merge_ExistingRow_UpdatesRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 2, GroupID = 2, Name = "Robert", Age = 41 };

            await Database.RunAsync(kind, x => x.MergeAsync(table, merged, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { TwoPersons[0], merged }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_NullMembers_WritesNulls(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var updated = new Person() { ID = 2, GroupID = 1, Name = null, Age = null };
            var inserted = new Person() { ID = 3, GroupID = 1, Name = null, Age = null };

            Database.Run(kind, x =>
            {
                x.Merge(table, updated, "ID");
                x.Merge(table, inserted, "ID");
            });

            ReadPersons(table).Should().BeEquivalentTo(new[] { TwoPersons[0], updated, inserted }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_CompositeKey_InsertsAndUpdates(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, new Membership() { GroupID = 1, Code = "A", Name = "first" });

            Database.Run(kind, x =>
            {
                x.Merge(table, new Membership() { GroupID = 1, Code = "A", Name = "updated" }, "GroupID", "Code");
                x.Merge(table, new Membership() { GroupID = 1, Code = "B", Name = "inserted" }, "GroupID", "Code");
            });

            ReadMemberships(table).Should().BeEquivalentTo(new[]
            {
                new Membership() { GroupID = 1, Code = "A", Name = "updated" },
                new Membership() { GroupID = 1, Code = "B", Name = "inserted" }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Merge_CompositeKey_InsertsAndUpdatesAsync(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, new Membership() { GroupID = 1, Code = "A", Name = "first" });

            await Database.RunAsync(kind, async x =>
            {
                await x.MergeAsync(table, new Membership() { GroupID = 1, Code = "A", Name = "updated" }, "GroupID", "Code");
                await x.MergeAsync(table, new Membership() { GroupID = 1, Code = "B", Name = "inserted" }, "GroupID", "Code");
            });

            ReadMemberships(table).Should().BeEquivalentTo(new[]
            {
                new Membership() { GroupID = 1, Code = "A", Name = "updated" },
                new Membership() { GroupID = 1, Code = "B", Name = "inserted" }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMerge_InsertAndUpdate_EachAffectOneRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);

            var updated = Database.Run(kind, x => x.CreateMerge(table, new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 31 }, "ID").ExecuteNonQuery());
            var inserted = Database.Run(kind, x => x.CreateMerge(table, new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = 50 }, "ID").ExecuteNonQuery());

            updated.Should().Be(1);
            inserted.Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMerge_InsertAndUpdate_EachAffectOneRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);

            var updated = await Database.RunAsync(kind, x => x.CreateMerge(table, new Person() { ID = 1, GroupID = 1, Name = "Alicia", Age = 31 }, "ID")
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            var inserted = await Database.RunAsync(kind, x => x.CreateMerge(table, new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = 50 }, "ID")
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            updated.Should().Be(1);
            inserted.Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_WithQueryOptions_UpdatesRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 1, GroupID = 3, Name = "Alicia", Age = 31 };

            Database.Run(kind, x => x.Merge(table, merged, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { merged, TwoPersons[1] }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Merge_WithQueryOptions_UpdatesRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);
            var merged = new Person() { ID = 1, GroupID = 3, Name = "Alicia", Age = 31 };

            await Database.RunAsync(kind, x => x.MergeAsync(table, merged, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { merged, TwoPersons[1] }, o => o.WithStrictOrdering());
        }

        // nothing to update: a missing row is inserted, an existing one stays as it is
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q09")]
        public void Merge_OnlyKeyMembers_InsertsIfMissing(ContextKind kind)
        {
            var table = CreateKeyAndNameTable();

            var affected = Database.Run(kind, x => new[]
            {
                x.CreateMerge(table, new { ID = 1 }, "ID").ExecuteNonQuery(),
                x.CreateMerge(table, new { ID = 2 }, "ID").ExecuteNonQuery()
            });

            affected.Should().Equal(0, 1);
            ReadKeysAndNames(table).Should().Equal("1:one", "2:");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q09")]
        public async Task Merge_OnlyKeyMembers_InsertsIfMissingAsync(ContextKind kind)
        {
            var table = CreateKeyAndNameTable();

            await Database.RunAsync(kind, async x =>
            {
                await x.MergeAsync(table, new { ID = 1 }, "ID");
                await x.MergeAsync(table, new { ID = 2 }, "ID");
            });

            ReadKeysAndNames(table).Should().Equal("1:one", "2:");
        }

        // a key in another case than the member matches it, and the text uses the member's name
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q125")]
        public void Merge_KeyInOtherCase_UpdatesMatchingRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, TwoPersons);

            Database.Run(kind, x => x.Merge(table, new Person() { ID = 2, GroupID = 1, Name = "Robert", Age = 41 }, "id"));

            ReadPersons(table).Select(x => x.Name).Should().Equal("Alice", "Robert");
        }

        // a key column and a nullable column with one row (1, 'one')
        private string CreateKeyAndNameTable()
        {
            var table = Database.CreateScratchTable($"{Q("ID")} int not null primary key, {Q("Name")} varchar(50) null");
            Database.Execute($"insert into {table} ({Q("ID")}, {Q("Name")}) values (1, 'one')");
            return table;
        }

        private string[] ReadKeysAndNames(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select {Q("ID")}, {Q("Name")} from {table} order by {Q("ID")}")
                .ExecuteQueryAnonymous(new { ID = 0, Name = "" }).Select(y => $"{y.ID}:{y.Name}").ToArray());
        }
    }
}
