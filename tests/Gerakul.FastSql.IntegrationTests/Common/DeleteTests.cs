using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I7: Delete, DeleteAsync and the rows affected by CreateDelete against scratch tables.
    // Concrete classes are in the provider folders.
    public abstract class DeleteTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected DeleteTests(TDatabase database)
            : base(database)
        {
        }

        private static Person[] ThreePersons => new[]
        {
            new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
            new Person() { ID = 2, GroupID = 1, Name = "Bob", Age = 40 },
            new Person() { ID = 3, GroupID = 2, Name = "Bob", Age = null }
        };

        private static Membership[] ThreeMemberships => new[]
        {
            new Membership() { GroupID = 1, Code = "A", Name = "first" },
            new Membership() { GroupID = 1, Code = "B", Name = "second" },
            new Membership() { GroupID = 2, Code = "B", Name = "third" }
        };

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_ByKey_DeletesMatchingRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Delete(table, new Person() { ID = 2, GroupID = 9, Name = "ignored" }, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Delete_ByKey_DeletesMatchingRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.DeleteAsync(table, new Person() { ID = 2, GroupID = 9, Name = "ignored" }, "ID"));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_CompositeKey_DeletesMatchingRow(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, ThreeMemberships);

            Database.Run(kind, x => x.Delete(table, new Membership() { GroupID = 1, Code = "B", Name = "ignored" }, "GroupID", "Code"));

            ReadMemberships(table).Should().BeEquivalentTo(ThreeMemberships.Where(m => m.Name != "second"), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Delete_CompositeKey_DeletesMatchingRowAsync(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, ThreeMemberships);

            await Database.RunAsync(kind, x => x.DeleteAsync(table, new Membership() { GroupID = 1, Code = "B", Name = "ignored" }, "GroupID", "Code"));

            ReadMemberships(table).Should().BeEquivalentTo(ThreeMemberships.Where(m => m.Name != "second"), o => o.WithStrictOrdering());
        }

        // without key fields every member is a key
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_NoKeys_UsesAllMembersAsKeys(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Delete(table, new { GroupID = 1, Name = "Bob" }));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Delete_NoKeys_UsesAllMembersAsKeysAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.DeleteAsync(table, new { GroupID = 1, Name = "Bob" }));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        // README: context.Delete("Employee", new { ID = 2 })
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_ReadmeShortcut_DeletesRowWithId(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Delete(table, new { ID = 2 }));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Delete_ReadmeShortcut_DeletesRowWithIdAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.DeleteAsync(table, new { ID = 2 }));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 2), o => o.WithStrictOrdering());
        }

        // a null key value is compared with "= NULL", which matches no row; here a row read back from the table
        // (all members are keys) cannot be deleted because its Age is null
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q08")]
        public void Delete_NullKeyValue_DeletesNothing(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var rowWithNull = ReadPersons(table).Single(p => p.ID == 3);

            var affected = Database.Run(kind, x => x.CreateDelete(table, rowWithNull).ExecuteNonQuery());
            Database.Run(kind, x => x.Delete(table, new { Age = (int?)null }, "Age"));

            affected.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q08")]
        public async Task Delete_NullKeyValue_DeletesNothingAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var rowWithNull = ReadPersons(table).Single(p => p.ID == 3);

            var affected = await Database.RunAsync(kind, x => x.CreateDelete(table, rowWithNull).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            await Database.RunAsync(kind, x => x.DeleteAsync(table, new { Age = (int?)null }, "Age"));

            affected.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateDelete_KeyMatchingSeveralRows_ReturnsRowsAffected(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = Database.Run(kind, x => x.CreateDelete(table, new { GroupID = 1 }).ExecuteNonQuery());
            var none = Database.Run(kind, x => x.CreateDelete(table, new { GroupID = 1 }).ExecuteNonQuery());

            affected.Should().Be(2);
            none.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID == 3));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateDelete_KeyMatchingSeveralRows_ReturnsRowsAffectedAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = await Database.RunAsync(kind, x => x.CreateDelete(table, new { GroupID = 1 }).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            var none = await Database.RunAsync(kind, x => x.CreateDelete(table, new { GroupID = 1 }).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(2);
            none.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID == 3));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_WithQueryOptions_DeletesMatchingRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Delete(table, new { ID = 1 }, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 1), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Delete_WithQueryOptions_DeletesMatchingRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.DeleteAsync(table, new { ID = 1 }, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons.Where(p => p.ID != 1), o => o.WithStrictOrdering());
        }
    }
}
