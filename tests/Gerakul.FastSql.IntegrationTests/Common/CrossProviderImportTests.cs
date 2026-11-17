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

        // SQL Server CreateTable takes the type names from the reader's column schema, here PostgreSQL ones:
        // integer is also a SQL Server type name, character varying is not
        [Fact]
        [Trait("UpgradeSensitive", "Npgsql column schema")]
        public void WriteToServer_CreateTableOnSqlServerFromPostgreSqlQuery_ThrowsDbException()
        {
            var table = NewTableName();

            Action act = () => SqlServer.Run(ContextKind.Connection, x => PostgreSqlContext().CreateSimple(PostgreSqlEmployeeQuery)
                .WriteToServer(x, table, new BulkOptions(createTable: true)));

            act.Should().Throw<DbException>().WithMessage("Column, parameter, or variable #2: Cannot find data type character varying.")
                .Which.GetType().Name.Should().Be("SqlException");
            TableExists(table).Should().BeFalse();
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

        private bool TableExists(string table)
        {
            return SqlServer.Run(ContextKind.ConnectionString, x => x.CreateSimple("select count(*) from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @p0", table)
                .ExecuteQueryFirstColumn<int>().Single()) == 1;
        }
    }
}
