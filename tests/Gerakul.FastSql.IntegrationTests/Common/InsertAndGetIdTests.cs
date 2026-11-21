using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // P2 (phase 5): CreateInsertAndGetID of Common on both providers, on the context types that CreateContext returns
    // (no cast to ISqlCommandCreator, Q18). The value is scope_identity() on SQL Server (decimal) and the inserted row's
    // serial or identity value on PostgreSQL (bigint); ExecuteScalar<long> and ExecuteQueryFirstColumn<int> read both.
    // Concrete classes: SqlServer/SqlServerInsertAndGetIdTests, PostgreSQL/PostgreSqlInsertAndGetIdTests
    public abstract class InsertAndGetIdTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected InsertAndGetIdTests(TDatabase database)
            : base(database)
        {
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_ExecuteScalarOfLong_ReturnsGeneratedIds(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = Database.Run(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 }).ExecuteScalar<long>());
            var second = Database.Run(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 2, Name = "Bob", Age = 40 }).ExecuteScalar<long>());

            first.Should().Be(1L);
            second.Should().Be(2L);
            ReadPersons(table).Should().BeEquivalentTo(new[]
            {
                new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
                new Person() { ID = 2, GroupID = 2, Name = "Bob", Age = 40 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertAndGetID_ExecuteScalarOfLong_ReturnsGeneratedIdsAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = await Database.RunAsync(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteScalarAsync<long>(TestContext.Current.CancellationToken));
            var second = await Database.RunAsync(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 2, Name = "Bob", Age = 40 })
                .ExecuteScalarAsync<long>(TestContext.Current.CancellationToken));

            first.Should().Be(1L);
            second.Should().Be(2L);
            ReadPersons(table).Should().HaveCount(2);
        }

        // the first result set is the one with the ID: the insert itself returns no rows
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_ExecuteQueryFirstColumn_ReturnsIdentityAsInt(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = Database.Run(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteQueryFirstColumn<int>().ToArray());

            ids.Should().Equal(1);
            ReadPersons(table).Should().HaveCount(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertAndGetID_ExecuteQueryFirstColumn_ReturnsIdentityAsIntAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = await Database.RunAsync(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            ids.Should().Equal(1);
            ReadPersons(table).Should().HaveCount(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_IgnoredIdentity_ReturnsGeneratedIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var id = Database.Run(kind, x => x.CreateInsertAndGetID(table, person, "ID").ExecuteScalar<int>());

            id.Should().Be(1);
            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_WithQueryOptions_ReturnsGeneratedIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var id = Database.Run(kind, x => x.CreateInsertAndGetID(table, person, new QueryOptions(30), "id").ExecuteScalar<long>());

            id.Should().Be(1L);
            ReadPersons(table).Should().HaveCount(1);
        }

        // a table without a generated key: the row is inserted and the ID is null, a row with DBNull on both providers
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_TableWithoutIdentity_ReturnsDBNull(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 5, GroupID = 1, Name = "Alice", Age = 30 };

            var id = Database.Run(kind, x => x.CreateInsertAndGetID(table, person).ExecuteScalar());

            id.Should().Be(DBNull.Value);
            Database.Run(kind, x => x.CreateInsertAndGetID(table, new Person() { ID = 6, GroupID = 1 }).ExecuteScalar<long?>()).Should().BeNull();
            ReadPersons(table).Select(x => x.ID).Should().Equal(5, 6);
        }

        [Fact]
        public void CompileInsertAndGetID_Precompiled_ReturnsGeneratedIdsForEachValue()
        {
            var table = CreatePersonTableWithIdentity();

            var ids = Database.Run(ContextKind.Connection, x =>
            {
                var insert = x.CommandCompilator.CompileInsertAndGetID<Person>(table, "ID");
                return new[] { "Alice", "Bob", "Carol" }
                    .Select(name => x.CreateMapped(insert, new Person() { GroupID = 1, Name = name }).ExecuteScalar<long>())
                    .ToArray();
            });

            ids.Should().Equal(1L, 2L, 3L);
            ReadPersons(table).Select(x => x.Name).Should().Equal("Alice", "Bob", "Carol");
        }
    }
}
