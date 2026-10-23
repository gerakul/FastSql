using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.PostgreSQL;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    // The shared bulk copy scenarios on PostgreSQL, plus PostgreSQL-only behavior: no table creation (Q10),
    // the synchronous async copy (Q05), quoted column names (Q19) and the binary COPY type inference
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlBulkCopyTests : BulkCopyTests<PostgreSqlDatabase>
    {
        private const string CreateTableError = "Table creation is not implemented for PostgreSQL";

        public PostgreSqlBulkCopyTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "serial primary key";

        protected override string TextColumnType => "varchar(50)";

        protected override string DateTimeColumnType => "timestamp";

        #region CreateTable

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q10")]
        public void WriteToServer_CreateTable_ThrowsNotImplementedException(ContextKind kind)
        {
            var table = NewTableName();

            Action act = () => Database.Run(kind, x => NamedRows.WriteToServer(x, table, new BulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            act.Should().Throw<NotImplementedException>().WithMessage(CreateTableError);
            TableExists(table).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q10")]
        public async Task WriteToServerAsync_CreateTable_ThrowsNotImplementedExceptionAsync(ContextKind kind)
        {
            var table = NewTableName();

            Func<Task> act = () => Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table,
                new BulkOptions(createTable: true, ignoreDataReaderSchemaTable: true), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<NotImplementedException>().WithMessage(CreateTableError);
            TableExists(table).Should().BeFalse();
        }

        // NpgsqlBulkOptions has no constructor argument for it, the property still works
        [Fact]
        [Trait("Quirk", "Q10")]
        public void WriteToServer_CreateTableSetOnNpgsqlBulkOptions_ThrowsNotImplementedException()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple(SourceQuery)
                .WriteToServer(x, table, new NpgsqlBulkOptions() { CreateTable = true }));

            act.Should().Throw<NotImplementedException>().WithMessage(CreateTableError);
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        [Trait("Quirk", "Q10")]
        public async Task WriteToServerAsync_CreateTableAtProviderLevel_ThrowsNotImplementedExceptionAsync()
        {
            var table = NewTableName();
            var provider = Spec.CreateProvider();
            provider.BulkOptions.CreateTable = true;

            Func<Task> act = () => Database.RunAsync(provider, ContextKind.Connection, x => SourceContext().CreateSimple(SourceQuery)
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<NotImplementedException>().WithMessage(CreateTableError);
            TableExists(table).Should().BeFalse();
        }

        #endregion

        #region Synchronous async copy

        // the copy runs to the end before WriteToServerAsync returns
        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q05")]
        public async Task WriteToServerAsync_ScopedContext_ReturnsCompletedTaskAsync(ContextKind kind)
        {
            var table = CreateNamedTable();
            Task copy = null;

            await Database.RunAsync(kind, x =>
            {
                copy = NamedRows.WriteToServerAsync(x, table, TestContext.Current.CancellationToken);
                copy.Status.Should().Be(TaskStatus.RanToCompletion);
                return copy;
            });

            ReadNames(table).Should().Equal("one", "two");
        }

        [Fact]
        [Trait("Quirk", "Q05")]
        public async Task WriteToServerAsync_ConnectionContext_CopiesRowsBeforeTaskIsAwaitedAsync()
        {
            var table = CreateNamedTable();

            using (var connection = await Database.OpenConnectionAsync())
            {
                var copy = NamedRows.WriteToServerAsync(Spec.DefaultProvider.CreateContext(connection), table, TestContext.Current.CancellationToken);

                ReadNames(table).Should().Equal("one", "two");
                await copy;
            }
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q05")]
        public async Task WriteToServerAsync_CancelledToken_CopiesRowsAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                await Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table, cancellation.Token));
            }

            ReadNames(table).Should().Equal("one", "two");
        }

        #endregion

        #region Column and table names

        // COPY quotes the column names, so the default Source selector matches them case-sensitively
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q19")]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public void WriteToServer_DefaultSelectorAndNamesDifferInCase_ThrowsPostgresException(ContextKind kind)
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(kind, x => new[] { new { ID = 1, name = "one" } }.WriteToServer(x, table));

            act.Should().Throw<PostgresException>().WithMessage($"42703: column \"name\" of relation \"{table}\" does not exist")
                .Which.SqlState.Should().Be("42703");
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q19")]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public async Task WriteToServerAsync_DefaultSelectorAndNamesDifferInCase_ThrowsPostgresExceptionAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            Func<Task> act = () => Database.RunAsync(kind, x => new[] { new { ID = 1, name = "one" } }
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<PostgresException>().WithMessage($"42703: column \"name\" of relation \"{table}\" does not exist"))
                .Which.SqlState.Should().Be("42703");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public void WriteToServer_DefaultSelectorAndSourceMemberNotInDestination_ThrowsPostgresException()
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(ContextKind.Connection, x => new[] { new { ID = 1, Name = "one", Extra = "extra" } }.WriteToServer(x, table));

            act.Should().Throw<PostgresException>().WithMessage($"42703: column \"Extra\" of relation \"{table}\" does not exist");
            CountRows(table).Should().Be(0);
        }

        // the copy asks the source reader for the ordinal of each field
        [Fact]
        public void WriteToServer_FieldNotInSource_ThrowsKeyNotFoundException()
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(ContextKind.Connection, x => NamedRows.WriteToServer(x, table, "ID", "Missing"));

            act.Should().Throw<KeyNotFoundException>().WithMessage("The given key 'Missing' was not present in the dictionary.");
            CountRows(table).Should().Be(0);
        }

        // the table name goes into COPY as given, so a PascalCase table needs quotes
        [Fact]
        [Trait("Quirk", "Q19")]
        [Trait("UpgradeSensitive", "Npgsql exception type")]
        public void WriteToServer_UnquotedPascalCaseTableName_ThrowsPostgresException()
        {
            var table = "Bulk_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Database.Execute($"create table \"{table}\" (\"ID\" int not null, \"Name\" varchar(50) null)");

            Action unquoted = () => Database.Run(ContextKind.Connection, x => NamedRows.WriteToServer(x, table));

            unquoted.Should().Throw<PostgresException>().WithMessage($"42P01: relation \"{table.ToLowerInvariant()}\" does not exist");

            Database.Run(ContextKind.Connection, x => NamedRows.WriteToServer(x, $"\"{table}\""));

            CountRows($"\"{table}\"").Should().Be(2);
        }

        #endregion

        #region Identity

        // there is no identity handling: values in the source are written as they are
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q111")]
        public void WriteToServer_IdentityColumnInSource_WritesSourceValues(ContextKind kind)
        {
            var table = Database.CreateScratchTable("\"ID\" serial primary key, \"Name\" varchar(50) null");

            Database.Run(kind, x => new[] { new { ID = 10, Name = "ten" }, new { ID = 20, Name = "twenty" } }.WriteToServer(x, table));

            ReadIds(table).Should().Equal(10, 20);
        }

        #endregion

        #region Binary COPY type inference

        // each value is written with the PostgreSQL type Npgsql infers from its CLR type, not with the column type
        [Fact]
        [Trait("Quirk", "Q85")]
        [Trait("UpgradeSensitive", "Npgsql binary COPY type inference")]
        public void WriteToServer_IntValueIntoBigintColumn_ThrowsPostgresException()
        {
            var table = Database.CreateScratchTable("\"ID\" bigint not null");

            Action act = () => Database.Run(ContextKind.Connection, x => new[] { new { ID = 1 } }.WriteToServer(x, table));

            act.Should().Throw<PostgresException>().WithMessage("08P01: insufficient data left in message").Which.SqlState.Should().Be("08P01");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("Quirk", "Q85")]
        [Trait("UpgradeSensitive", "Npgsql binary COPY type inference")]
        public async Task WriteToServerAsync_IntValueIntoBigintColumn_ThrowsPostgresExceptionAsync()
        {
            var table = Database.CreateScratchTable("\"ID\" bigint not null");

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => new[] { new { ID = 1 } }
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<PostgresException>().WithMessage("08P01: insufficient data left in message")).Which.SqlState.Should().Be("08P01");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("Quirk", "Q85")]
        [Trait("UpgradeSensitive", "Npgsql binary COPY type inference")]
        public void WriteToServer_DateColumnFromQuery_ThrowsPostgresException()
        {
            var table = Database.CreateScratchTable("\"ID\" int not null, \"DateOfFoundation\" date not null");

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext()
                .CreateSimple("select \"ID\", \"DateOfFoundation\" from \"Company\"").WriteToServer(x, table));

            act.Should().Throw<PostgresException>().WithMessage("22P03: incorrect binary data format").Which.SqlState.Should().Be("22P03");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("UpgradeSensitive", "Npgsql binary COPY type inference")]
        public void WriteToServer_ValuesMatchingColumnTypes_CopiesRows()
        {
            var table = Database.CreateScratchTable("\"ID\" bigint not null, \"DateOfFoundation\" timestamp not null");

            Database.Run(ContextKind.Connection, x => new[] { new { ID = 1L, DateOfFoundation = new DateTime(1990, 6, 8) } }.WriteToServer(x, table));

            CountRows(table).Should().Be(1);
        }

        #endregion

        #region Helpers

        private const string SourceQuery = "select \"ID\", \"Name\" from \"Company\"";

        private static NamedRow[] NamedRows => new[]
        {
            new NamedRow() { ID = 1, Name = "one" },
            new NamedRow() { ID = 2, Name = "two" }
        };

        private static string NewTableName()
        {
            return "bulk_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private DbContext SourceContext()
        {
            return Spec.DefaultProvider.CreateContext(Database.ConnectionString);
        }

        private string CreateNamedTable()
        {
            return Database.CreateScratchTable("\"ID\" int not null, \"Name\" varchar(50) null");
        }

        private bool TableExists(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select count(*) from information_schema.tables where table_name = @p0", table)
                .ExecuteQueryFirstColumn<int>().Single()) == 1;
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single());
        }

        private int[] ReadIds(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select \"ID\" from {table} order by \"ID\"")
                .ExecuteQueryFirstColumn<int>().ToArray());
        }

        private string[] ReadNames(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select \"Name\" from {table} order by \"ID\"")
                .ExecuteQueryFirstColumn<string>().ToArray());
        }

        private class NamedRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        #endregion
    }
}
