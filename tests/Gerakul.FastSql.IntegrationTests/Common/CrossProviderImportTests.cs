using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I9: data import between the providers through IWrappedCommand.WriteToServer, a query of one provider written into
    // a table of the other. Needs both databases, so the class is not split per provider.
    [Trait("Provider", "SqlServer")]
    [Trait("Provider", "PostgreSQL")]
    public class CrossProviderImportTests : IClassFixture<SqlServerDatabase>, IClassFixture<PostgreSqlDatabase>
    {
        private const string SqlServerEmployeeQuery = "select CompanyID, Name, Phone, Age, StartWorking from Employee order by ID";
        private const string PostgreSqlEmployeeQuery = "select \"CompanyID\", \"Name\", \"Phone\", \"Age\", \"StartWorking\" from \"Employee\" order by \"ID\"";

        public CrossProviderImportTests(SqlServerDatabase sqlServer, PostgreSqlDatabase postgreSql)
        {
            this.SqlServer = sqlServer;
            this.PostgreSql = postgreSql;
        }

        private SqlServerDatabase SqlServer { get; }

        private PostgreSqlDatabase PostgreSql { get; }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_SqlServerQueryIntoPostgreSqlTable_CopiesRows(ContextKind kind)
        {
            var table = CreatePostgreSqlEmployeeTable();

            PostgreSql.Run(kind, x => SqlServerContext().CreateSimple(SqlServerEmployeeQuery).WriteToServer(x, table));

            ReadEmployees(PostgreSql, table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_SqlServerQueryIntoPostgreSqlTable_CopiesRowsAsync(ContextKind kind)
        {
            var table = CreatePostgreSqlEmployeeTable();

            await PostgreSql.RunAsync(kind, x => SqlServerContext().CreateSimple(SqlServerEmployeeQuery)
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(PostgreSql, table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_PostgreSqlQueryIntoSqlServerTable_CopiesRows(ContextKind kind)
        {
            var table = CreateSqlServerEmployeeTable();

            SqlServer.Run(kind, x => PostgreSqlContext().CreateSimple(PostgreSqlEmployeeQuery).WriteToServer(x, table));

            ReadEmployees(SqlServer, table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_PostgreSqlQueryIntoSqlServerTable_CopiesRowsAsync(ContextKind kind)
        {
            var table = CreateSqlServerEmployeeTable();

            await SqlServer.RunAsync(kind, x => PostgreSqlContext().CreateSimple(PostgreSqlEmployeeQuery)
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(SqlServer, table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        // any IWrappedCommand is a source; the procedure returns ID too, which PostgreSQL writes as it is
        [Fact]
        public void WriteToServer_SqlServerProcedureIntoPostgreSqlTable_CopiesRows()
        {
            var table = CreatePostgreSqlEmployeeTable();

            PostgreSql.Run(ContextKind.Connection, x => SqlServerContext().CreateProcedure("TestProc", new { CompanyID = 1, Age = 20 }).WriteToServer(x, table));

            ReadEmployees(PostgreSql, table).Should().BeEquivalentTo(new[] { SeedData.Employees[0], SeedData.Employees[2] }, o => o.WithStrictOrdering());
        }

        // P7 (phase 5): SQL Server CreateTable takes the column types from the reader's column schema only when SqlClient
        // made it; a PostgreSQL schema has PostgreSQL type names ("character varying" failed on SQL Server), so the
        // field types decide, as with IgnoreDataReaderSchemaTable
        [Fact]
        [Trait("Fixed", "Q131")]
        public void WriteToServer_CreateTableOnSqlServerFromPostgreSqlQuery_CreatesTableFromFieldTypes()
        {
            var table = NewTableName();

            SqlServer.Run(ContextKind.Connection, x => PostgreSqlContext().CreateSimple("select \"ID\", \"CompanyID\", \"Name\" from \"Employee\" order by \"ID\"")
                .WriteToServer(x, table, new BulkOptions(createTable: true)));

            SqlServer.Run(ContextKind.ConnectionString, x => x.CreateSimple("select DATA_TYPE + coalesce('(' + cast(CHARACTER_MAXIMUM_LENGTH as varchar) + ')', '') "
                    + "from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = @p0 order by ORDINAL_POSITION", table).ExecuteQueryFirstColumn<string>().ToArray())
                .Should().Equal("int", "int", "nvarchar(4000)");
            SqlServer.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by ID").ExecuteQueryAnonymous(new { ID = 0, CompanyID = 0, Name = "" }).ToArray())
                .Should().BeEquivalentTo(SeedData.Employees.Select(x => new { x.ID, x.CompanyID, x.Name }), o => o.WithStrictOrdering());
        }

        // the mirror case (P1, P7): PostgreSQL CreateTable takes the column types from the field types of a SQL Server reader;
        // a value type is not null (Q104), so the query leaves out the rows without StartWorking
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q131")]
        public void WriteToServer_CreateTableOnPostgreSqlFromSqlServerQuery_CreatesTableFromFieldTypes(ContextKind kind)
        {
            var table = NewTableName();

            PostgreSql.Run(kind, x => SqlServerContext().CreateSimple("select ID, CompanyID, Name, StartWorking from Employee where StartWorking is not null order by ID")
                .WriteToServer(x, table, new BulkOptions(createTable: true)));

            PostgreSql.Run(ContextKind.ConnectionString, x => x.CreateSimple("select data_type || coalesce('(' || character_maximum_length || ')', '') || ' ' || is_nullable "
                    + "from information_schema.columns where table_name = @p0 order by ordinal_position", table).ExecuteQueryFirstColumn<string>().ToArray())
                .Should().Equal("integer NO", "integer NO", "character varying(4000) YES", "timestamp without time zone NO");
            PostgreSql.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by \"ID\"")
                    .ExecuteQueryAnonymous(new { ID = 0, CompanyID = 0, Name = "", StartWorking = (DateTime?)null }).ToArray())
                .Should().BeEquivalentTo(SeedData.Employees.Where(x => x.StartWorking != null).Select(x => new { x.ID, x.CompanyID, x.Name, x.StartWorking }),
                    o => o.WithStrictOrdering());
        }

        [Fact]
        public void WriteToServer_CreateTableOnSqlServerFromPostgreSqlQueryIgnoringSchemaTable_CreatesTableFromFieldTypes()
        {
            var table = NewTableName();

            SqlServer.Run(ContextKind.Connection, x => PostgreSqlContext().CreateSimple("select \"ID\", \"CompanyID\", \"Name\" from \"Employee\" order by \"ID\"")
                .WriteToServer(x, table, new BulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            SqlServer.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by ID").ExecuteQueryAnonymous(new { ID = 0, CompanyID = 0, Name = "" }).ToArray())
                .Should().BeEquivalentTo(SeedData.Employees.Select(x => new { x.ID, x.CompanyID, x.Name }), o => o.WithStrictOrdering());
        }

        // P3 (phase 5): SQL Server datetimeoffset values keep their offsets, which Npgsql refuses for timestamptz; the copy
        // writes them in UTC, so the instants arrive (the offsets do not, D2)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q87")]
        public void WriteToServer_SqlServerDatetimeoffsetIntoPostgreSqlTimestamptz_CopiesInstants(ContextKind kind)
        {
            var table = PostgreSql.CreateScratchTable("\"ID\" int not null, \"Moment\" timestamptz null");

            PostgreSql.Run(kind, x => SqlServerContext().CreateSimple("select ID, Moment from (values (1, cast('2020-01-02 03:04:05 +02:00' as datetimeoffset)), "
                    + "(2, cast('2020-01-02 03:04:05 -05:30' as datetimeoffset)), (3, null)) v (ID, Moment) order by ID")
                .WriteToServer(x, table));

            PostgreSql.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select \"Moment\" from {table} order by \"ID\"").ExecuteQueryFirstColumn<DateTimeOffset?>().ToArray())
                .Should().Equal(new DateTimeOffset(2020, 1, 2, 1, 4, 5, TimeSpan.Zero), new DateTimeOffset(2020, 1, 2, 8, 34, 5, TimeSpan.Zero), null);
        }

        private DbContext SqlServerContext()
        {
            return SqlServer.Spec.DefaultProvider.CreateContext(SqlServer.ConnectionString);
        }

        private DbContext PostgreSqlContext()
        {
            return PostgreSql.Spec.DefaultProvider.CreateContext(PostgreSql.ConnectionString);
        }

        private string CreateSqlServerEmployeeTable()
        {
            return SqlServer.CreateScratchTable("ID int identity(1,1) not null primary key, CompanyID int not null, Name nvarchar(50) not null, "
                + "Phone nvarchar(50) null, Age int null, StartWorking datetime2 null");
        }

        private string CreatePostgreSqlEmployeeTable()
        {
            return PostgreSql.CreateScratchTable("\"ID\" serial primary key, \"CompanyID\" int not null, \"Name\" varchar(50) not null, "
                + "\"Phone\" varchar(50) null, \"Age\" int null, \"StartWorking\" timestamp null");
        }

        private static string NewTableName()
        {
            return "bulk_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static Employee[] ReadEmployees(DatabaseFixture database, string table)
        {
            return database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {database.Spec.Quote("ID")}")
                .ExecuteQuery<Employee>().ToArray());
        }
    }
}
