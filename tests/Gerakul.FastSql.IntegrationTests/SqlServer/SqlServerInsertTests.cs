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
    public class SqlServerInsertTests : InsertTests<SqlServerDatabase>
    {
        public SqlServerInsertTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);

        protected override string DefaultSchema => "dbo";

        protected override string ExplicitIdentityValueMessage(string table)
        {
            return $"Cannot insert explicit value for identity column in table '{table}' when IDENTITY_INSERT is set to OFF.";
        }

        // column names are case-insensitive on SQL Server (the default collation), unlike PostgreSQL (Q19)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_MembersInOtherCaseThanColumns_InsertsRow(ContextKind kind)
        {
            var table = CreatePersonTable();

            Database.Run(kind, x => x.Insert(table, new { id = 1, groupid = 2, NAME = "Alice", age = 30 }));

            ReadPersons(table).Should().BeEquivalentTo(new[] { new Person() { ID = 1, GroupID = 2, Name = "Alice", Age = 30 } });
        }
    }
}
