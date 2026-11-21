using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    // I7: CreateInsertAndGetID on SQL Server: "insert ...; select scope_identity();". The scenarios of both providers
    // are in Common/InsertAndGetIdTests (P2, phase 5); here the decimal of scope_identity() (Q18), also through the
    // SQL Server method on ISqlCommandCreator, which forwards to the one of Common.
    [Trait("Provider", "SqlServer")]
    public class SqlServerInsertAndGetIdTests : InsertAndGetIdTests<SqlServerDatabase>
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

        // the method of Common, without the cast, runs the same command
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_CommonMethod_ReturnsIdentityAsDecimal(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            var id = Database.Run(kind, x => x.CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 }).ExecuteScalar());

            id.Should().BeOfType<decimal>().Which.Should().Be(1m);
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
    }
}
