using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    // I7: CreateInsertAndGetID (SQL Server only): "insert ...; select scope_identity();".
    // CreateContext returns the base context types, so the method needs a cast to ISqlCommandCreator (Q18).
    [Trait("Provider", "SqlServer")]
    public class SqlServerInsertAndGetIdTests : DataModificationTests<SqlServerDatabase>
    {
        public SqlServerInsertAndGetIdTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);

        // scope_identity() is numeric(38, 0), so the identity of an int column comes back as decimal
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q18")]
        public void CreateInsertAndGetID_ExecuteScalar_ReturnsIdentityAsDecimal(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 }).ExecuteScalar());
            var second = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 2, Name = "Bob", Age = 40 }).ExecuteScalar());

            first.Should().BeOfType<decimal>().Which.Should().Be(1m);
            second.Should().BeOfType<decimal>().Which.Should().Be(2m);
            ReadPersons(table).Should().BeEquivalentTo(new[]
            {
                new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
                new Person() { ID = 2, GroupID = 2, Name = "Bob", Age = 40 }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q18")]
        public async Task CreateInsertAndGetID_ExecuteScalar_ReturnsIdentityAsDecimalAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var first = await Database.RunAsync(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteScalarAsync(TestContext.Current.CancellationToken));
            var second = await Database.RunAsync(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 2, Name = "Bob", Age = 40 })
                .ExecuteScalarAsync(TestContext.Current.CancellationToken));

            first.Should().BeOfType<decimal>().Which.Should().Be(1m);
            second.Should().BeOfType<decimal>().Which.Should().Be(2m);
            ReadPersons(table).Should().HaveCount(2);
        }

        // the first result set is the one of "select scope_identity()": the insert itself returns no rows
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_ExecuteQueryFirstColumn_ReturnsIdentityAsInt(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
                .ExecuteQueryFirstColumn<int>().ToArray());

            ids.Should().Equal(1);
            ReadPersons(table).Should().HaveCount(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateInsertAndGetID_ExecuteQueryFirstColumn_ReturnsIdentityAsIntAsync(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var ids = await Database.RunAsync(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 })
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

            var id = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, person, "ID").ExecuteScalar());

            id.Should().Be(1m);
            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 } });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_WithQueryOptions_ReturnsGeneratedIdentity(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            var id = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, person, new QueryOptions(30), "ID").ExecuteScalar());

            id.Should().Be(1m);
            ReadPersons(table).Should().HaveCount(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_IdentityNotIgnored_ThrowsServerError(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();
            var person = new Person() { ID = 100, GroupID = 1, Name = "Alice", Age = 30 };

            ShouldThrowServerError(() => Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, person).ExecuteScalar()),
                $"Cannot insert explicit value for identity column in table '{table}' when IDENTITY_INSERT is set to OFF.");
        }

        // a table without an identity column: the row is inserted and scope_identity() is null
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_TableWithoutIdentity_ReturnsDBNull(ContextKind kind)
        {
            var table = CreatePersonTable();
            var person = new Person() { ID = 5, GroupID = 1, Name = "Alice", Age = 30 };

            var id = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, person).ExecuteScalar());

            id.Should().Be(DBNull.Value);
            ReadPersons(table).Should().BeEquivalentTo(new[] { person });
        }
    }
}
