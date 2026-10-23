using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I7: Update, UpdateAsync and the rows affected by CreateUpdate against scratch tables.
    // Concrete classes are in the provider folders.
    public abstract class UpdateTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected UpdateTests(TDatabase database)
            : base(database)
        {
        }

        // stable part of the server message for "update t set  where ..." (no non-key members, Q09)
        protected abstract string EmptySetClauseMessage { get; }

        // stable part of the server message for a key that is not a member: its parameter is never added
        protected abstract string UnmappedKeyParameterMessage { get; }

        private static Person[] ThreePersons => new[]
        {
            new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
            new Person() { ID = 2, GroupID = 1, Name = "Bob", Age = 40 },
            new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = 50 }
        };

        private static Membership[] ThreeMemberships => new[]
        {
            new Membership() { GroupID = 1, Code = "A", Name = "first" },
            new Membership() { GroupID = 1, Code = "B", Name = "second" },
            new Membership() { GroupID = 2, Code = "B", Name = "third" }
        };

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_SingleKey_UpdatesMatchingRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var updated = new Person() { ID = 2, GroupID = 3, Name = "Robert", Age = 41 };

            Database.Run(kind, x => x.Update(table, updated, "ID"));

            var expected = ThreePersons;
            expected[1] = updated;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Update_SingleKey_UpdatesMatchingRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var updated = new Person() { ID = 2, GroupID = 3, Name = "Robert", Age = 41 };

            await Database.RunAsync(kind, x => x.UpdateAsync(table, updated, "ID"));

            var expected = ThreePersons;
            expected[1] = updated;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_CompositeKey_UpdatesMatchingRow(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, ThreeMemberships);
            var updated = new Membership() { GroupID = 1, Code = "B", Name = "updated" };

            Database.Run(kind, x => x.Update(table, updated, "GroupID", "Code"));

            var expected = ThreeMemberships;
            expected[1] = updated;
            ReadMemberships(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Update_CompositeKey_UpdatesMatchingRowAsync(ContextKind kind)
        {
            var table = CreateMembershipTable();
            InsertMemberships(table, ThreeMemberships);
            var updated = new Membership() { GroupID = 1, Code = "B", Name = "updated" };

            await Database.RunAsync(kind, x => x.UpdateAsync(table, updated, "GroupID", "Code"));

            var expected = ThreeMemberships;
            expected[1] = updated;
            ReadMemberships(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateUpdate_KeyMatchingSeveralRows_ReturnsRowsAffected(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = Database.Run(kind, x => x.CreateUpdate(table, new { GroupID = 1, Age = 99 }, "GroupID").ExecuteNonQuery());

            affected.Should().Be(2);
            var expected = ThreePersons;
            expected[0].Age = 99;
            expected[1].Age = 99;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateUpdate_KeyMatchingSeveralRows_ReturnsRowsAffectedAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = await Database.RunAsync(kind, x => x.CreateUpdate(table, new { GroupID = 1, Age = 99 }, "GroupID")
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateUpdate_NoMatchingRow_ReturnsZero(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = Database.Run(kind, x => x.CreateUpdate(table, new Person() { ID = 4, GroupID = 1, Name = "Dave" }, "ID").ExecuteNonQuery());

            affected.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateUpdate_NoMatchingRow_ReturnsZeroAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var affected = await Database.RunAsync(kind, x => x.CreateUpdate(table, new Person() { ID = 4, GroupID = 1, Name = "Dave" }, "ID")
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            affected.Should().Be(0);
            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_NullMembers_WritesNulls(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var updated = new Person() { ID = 1, GroupID = 1, Name = null, Age = null };

            Database.Run(kind, x => x.Update(table, updated, "ID"));

            var expected = ThreePersons;
            expected[0] = updated;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Update_NullMembers_WritesNullsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);
            var updated = new Person() { ID = 1, GroupID = 1, Name = null, Age = null };

            await Database.RunAsync(kind, x => x.UpdateAsync(table, updated, "ID"));

            var expected = ThreePersons;
            expected[0] = updated;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        // README style: only the members of the anonymous type are set
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_AnonymousTypeWithSomeMembers_SetsOnlyThoseColumns(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Update(table, new { ID = 2, Name = "Robert" }, "ID"));

            var expected = ThreePersons;
            expected[1].Name = "Robert";
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Update_AnonymousTypeWithSomeMembers_SetsOnlyThoseColumnsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.UpdateAsync(table, new { ID = 2, Name = "Robert" }, "ID"));

            var expected = ThreePersons;
            expected[1].Name = "Robert";
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_WithQueryOptions_UpdatesMatchingRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Database.Run(kind, x => x.Update(table, new { ID = 3, Age = 51 }, new QueryOptions(30), "ID"));

            var expected = ThreePersons;
            expected[2].Age = 51;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Update_WithQueryOptions_UpdatesMatchingRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await Database.RunAsync(kind, x => x.UpdateAsync(table, new { ID = 3, Age = 51 }, new QueryOptions(30), "ID"));

            var expected = ThreePersons;
            expected[2].Age = 51;
            ReadPersons(table).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q09")]
        public void Update_OnlyKeyMembers_ThrowsServerError(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            ShouldThrowServerError(() => Database.Run(kind, x => x.Update(table, new { ID = 1 }, "ID")), EmptySetClauseMessage);

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q09")]
        public async Task Update_OnlyKeyMembers_ThrowsServerErrorAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            await ShouldThrowServerErrorAsync(() => Database.RunAsync(kind, x => x.UpdateAsync(table, new { ID = 1 }, "ID")), EmptySetClauseMessage);

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        // the key goes into the where clause, but only members become parameters
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_KeyThatIsNotAMember_ThrowsServerError(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            ShouldThrowServerError(() => Database.Run(kind, x => x.Update(table, new { Name = "Robert" }, "ID")), UnmappedKeyParameterMessage);

            ReadPersons(table).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }
    }
}
