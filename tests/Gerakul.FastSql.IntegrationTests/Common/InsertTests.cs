using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I7: Insert and InsertAsync against scratch tables. Concrete classes are in the provider folders.
    public abstract class InsertTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected InsertTests(TDatabase database)
            : base(database)
        {
        }

        protected abstract string DefaultSchema { get; }

        // stable part of the server message for an explicit value in the identity column
        protected abstract string ExplicitIdentityValueMessage(string table);

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_AllMembers_InsertsRow(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 7, GroupID = 1, Name = "Alice", Age = 30 };

            Database.Run(kind, x => x.Insert(table, person));

            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_AllMembers_InsertsRowAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 7, GroupID = 1, Name = "Alice", Age = 30 };

            await Database.RunAsync(kind, x => x.InsertAsync(table, person));

            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_IdentityIgnored_ServerGeneratesIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            Database.Run(kind, x =>
            {
                x.Insert(table, new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 }, "ID");
                x.Insert(table, new Person() { ID = 100, GroupID = 2, Name = "Bob", Age = 40 }, "ID");
            });

            ReadPersons(table).Should().BeEquivalentTo(new[]
            {
                new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
                new Person() { ID = 2, GroupID = 2, Name = "Bob", Age = 40 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_IdentityIgnored_ServerGeneratesIdentityAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            await Database.RunAsync(kind, async x =>
            {
                await x.InsertAsync(table, new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 }, "ID");
                await x.InsertAsync(table, new Person() { ID = 100, GroupID = 2, Name = "Bob", Age = 40 }, "ID");
            });

            ReadPersons(table).Should().BeEquivalentTo(new[]
            {
                new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
                new Person() { ID = 2, GroupID = 2, Name = "Bob", Age = 40 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_IdentityNotIgnored_ThrowsServerError(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            ShouldThrowServerError(() => Database.Run(kind, x => x.Insert(table, new Person() { ID = 100, GroupID = 1, Name = "Alice" })),
                ExplicitIdentityValueMessage(table));

            ReadPersons(table).Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_IdentityNotIgnored_ThrowsServerErrorAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            await ShouldThrowServerErrorAsync(() => Database.RunAsync(kind, x => x.InsertAsync(table, new Person() { ID = 100, GroupID = 1, Name = "Alice" })),
                ExplicitIdentityValueMessage(table));

            ReadPersons(table).Should().BeEmpty();
        }

        // the ignore list is compared with member names case-sensitively, so "id" does not ignore the ID member
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q90")]
        public void Insert_IgnoredFieldInOtherCase_IsNotIgnored(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            ShouldThrowServerError(() => Database.Run(kind, x => x.Insert(table, new Person() { ID = 100, GroupID = 1, Name = "Alice" }, "id")),
                ExplicitIdentityValueMessage(table));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_NullMembers_WritesNulls(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 1, GroupID = 1, Name = null, Age = null };

            Database.Run(kind, x => x.Insert(table, person));

            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_NullMembers_WritesNullsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 1, GroupID = 1, Name = null, Age = null };

            await Database.RunAsync(kind, x => x.InsertAsync(table, person));

            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_WithQueryOptions_InsertsRow(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            Database.Run(kind, x => x.Insert(table, new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 }, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_WithQueryOptions_InsertsRowAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            await Database.RunAsync(kind, x => x.InsertAsync(table, new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 }, new QueryOptions(30), "ID"));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        // members missing from the anonymous type are left to the column defaults
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_AnonymousType_InsertsItsMembers(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            Database.Run(kind, x => x.Insert(table, new { GroupID = 3, Name = "Carol" }));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 3, Name = "Carol", Age = null } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Insert_AnonymousType_InsertsItsMembersAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            await Database.RunAsync(kind, x => x.InsertAsync(table, new { GroupID = 3, Name = "Carol" }));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 3, Name = "Carol", Age = null } });
        }

        // the table name is used verbatim, so every form the server understands works
        [Theory]
        [InlineData("{table}")]
        [InlineData("{schema}.{table}")]
        [InlineData("{quotedTable}")]
        [InlineData("{schema}.{quotedTable}")]
        [InlineData("{quotedSchema}.{quotedTable}")]
        public void Insert_TableNameForm_InsertsRow(string form)
        {
            var table = CreatePersonTable();
            var tableName = form
                .Replace("{quotedSchema}", Q(DefaultSchema))
                .Replace("{quotedTable}", Q(table))
                .Replace("{schema}", DefaultSchema)
                .Replace("{table}", table);
            var person = new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 };

            Database.Run(ContextKind.ConnectionString, x => x.Insert(tableName, person));

            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }
    }
}
