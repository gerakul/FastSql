using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I7: CreateInsertWithOutput (4 overloads) against scratch tables with a generated ID.
    // SQL Server uses "output inserted.[x]", PostgreSQL "returning "x"". Concrete classes are in the provider folders.
    public abstract class InsertWithOutputTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected InsertWithOutputTests(TDatabase database)
            : base(database)
        {
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_OneOutputField_ReturnsGeneratedIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = 30 }, "ID")
                .ExecuteQueryFirstColumn<int>().ToArray());
            var second = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 2, Name = "Bob", Age = 40 }, "ID")
                .ExecuteQueryFirstColumn<int>().ToArray());

            first.Should().Equal(1);
            second.Should().Equal(2);
            ReadPersons(table).Should().BeEquivalentTo(new[]
            {
                new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
                new Person() { ID = 2, GroupID = 2, Name = "Bob", Age = 40 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_OneOutputField_ReturnsGeneratedIdentityAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = 30 }, "ID")
                .ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));
            var second = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 2, Name = "Bob", Age = 40 }, "ID")
                .ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            first.Should().Equal(1);
            second.Should().Equal(2);
            ReadPersons(table).Should().HaveCount(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_SeveralOutputFields_ReturnsInsertedRow(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var rows = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = (int?)null }, "ID", "GroupID", "Name", "Age")
                .ExecuteQuery<Person>().ToArray());

            var expected = new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = null } };
            rows.Should().BeEquivalentTo(expected);
            ReadPersons(table).Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_SeveralOutputFields_ReturnsInsertedRowAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var rows = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = (int?)null }, "ID", "GroupID", "Name", "Age")
                .ExecuteQueryAsync<Person>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            var expected = new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = null } };
            rows.Should().BeEquivalentTo(expected);
            ReadPersons(table).Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_WithQueryOptions_ReturnsGeneratedIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice" }, new QueryOptions(30), "ID")
                .ExecuteQueryFirstColumn<int>().ToArray());

            ids.Should().Equal(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_WithQueryOptions_ReturnsGeneratedIdentityAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice" }, new QueryOptions(30), "ID")
                .ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            ids.Should().Equal(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_IgnoredFields_InsertsOtherMembers(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var ids = Database.Run(kind, x => x.CreateInsertWithOutput(table, person, new List<string>() { "ID", "Age" }, "ID")
                .ExecuteQueryFirstColumn<int>().ToArray());

            ids.Should().Equal(1);
            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = null } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_IgnoredFields_InsertsOtherMembersAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var ids = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, person, new List<string>() { "ID", "Age" }, "ID")
                .ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            ids.Should().Equal(1);
            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = null } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_WithQueryOptionsAndIgnoredFields_InsertsOtherMembers(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var rows = Database.Run(kind, x => x.CreateInsertWithOutput(table, person, new QueryOptions(30), new List<string>() { "ID" }, "ID", "GroupID", "Name", "Age")
                .ExecuteQuery<Person>().ToArray());

            rows.Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_WithQueryOptionsAndIgnoredFields_InsertsOtherMembersAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var rows = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, person, new QueryOptions(30), new List<string>() { "ID" }, "ID", "GroupID", "Name", "Age")
                .ExecuteQueryAsync<Person>(cancellationToken: TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            rows.Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_NullIgnoreList_InsertsAllMembers(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = 30 }, (IList<string>)null, "ID")
                .ExecuteQueryFirstColumn<int>().ToArray());

            ids.Should().Equal(1);
            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        // without output fields the command is a plain insert: one row affected, no result set
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertWithOutput_NoOutputFields_IsPlainInsert(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var affected = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = 30 }).ExecuteNonQuery());
            var rows = Database.Run(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 2, Name = "Bob", Age = 40 }).ExecuteQuery().ToArray());

            affected.Should().Be(1);
            rows.Should().BeEmpty();
            ReadPersons(table).Should().HaveCount(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertWithOutput_NoOutputFields_IsPlainInsertAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var affected = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            var rows = await Database.RunAsync(kind, x => x.CreateInsertWithOutput(table, new { GroupID = 2, Name = "Bob", Age = 40 })
                .ExecuteQueryAsync(TestContext.Current.CancellationToken)
                .ToListCompat(TestContext.Current.CancellationToken));

            affected.Should().Be(1);
            rows.Should().BeEmpty();
            ReadPersons(table).Should().HaveCount(2);
        }
    }
}
