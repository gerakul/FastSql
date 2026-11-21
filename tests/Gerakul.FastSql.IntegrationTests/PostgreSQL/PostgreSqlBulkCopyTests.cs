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
    // The shared bulk copy scenarios on PostgreSQL, plus PostgreSQL-only behavior: the timeout of the binary import (P5),
    // the asynchronous copy (Q05, fixed), quoted column names (Q19) and the binary COPY type inference. Table creation
    // (Q10, fixed in phase 5) is in PostgreSqlCreateTableTests
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlBulkCopyTests : BulkCopyTests<PostgreSqlDatabase>
    {
        public PostgreSqlBulkCopyTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "serial primary key";

        protected override string TextColumnType => "varchar(50)";

        protected override string DateTimeColumnType => "timestamp";

        #region Timeout (P5)

        // BulkCopyTimeout sets the timeout of the binary import; when it expires Npgsql throws and closes the connection
        [Theory]
        [InlineData(ContextKind.ConnectionString)]
        [InlineData(ContextKind.Connection)]
        public void WriteToServer_TimeoutShorterThanCopy_ThrowsNpgsqlException(ContextKind kind)
        {
            var table = CreateSlowTable();

            Action act = () => Database.Run(kind, x => NamedRows.Take(1).WriteToServer(x, table, new NpgsqlBulkOptions(bulkCopyTimeout: 1)));

            act.Should().Throw<NpgsqlException>().WithInnerException<TimeoutException>();
        }

        [Fact]
        public async Task WriteToServerAsync_TimeoutShorterThanCopy_ThrowsNpgsqlExceptionAsync()
        {
            var table = CreateSlowTable();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => NamedRows.Take(1).WriteToServerAsync(x, table,
                new BulkOptions(bulkCopyTimeout: 1), TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<NpgsqlException>()).WithInnerException<TimeoutException>();
        }

        // 0 is no limit, as for SqlBulkCopy; the base BulkOptions carry the timeout too
        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        public void WriteToServer_TimeoutLongerThanCopyOrZero_CopiesRows(int timeout)
        {
            var table = CreateSlowTable();

            Database.Run(ContextKind.Connection, x => NamedRows.Take(1).WriteToServer(x, table, new BulkOptions(bulkCopyTimeout: timeout)));

            CountRows(table).Should().Be(1);
        }

        #endregion

        #region Asynchronous copy

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q05")]
        public async Task WriteToServerAsync_CopiesRowsAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            await Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadNames(table).Should().Equal("one", "two");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q05")]
        public async Task WriteToServerAsync_CancelledToken_ThrowsAndCopiesNothingAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Func<Task> act = () => Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table, cancellation.Token));

                await act.Should().ThrowAsync<OperationCanceledException>();
            }

            CountRows(table).Should().Be(0);
        }

        // the token is checked for every row, and the import is not completed
        [Fact]
        [Trait("Fixed", "Q05")]
        public async Task WriteToServerAsync_TokenCancelledDuringCopy_ThrowsAndCopiesNothingAsync()
        {
            var table = CreateNamedTable();

            using (var cancellation = new CancellationTokenSource())
            {
                var rows = NamedRows.Select((x, i) =>
                {
                    if (i == 1)
                    {
                        cancellation.Cancel();
                    }

                    return x;
                });

                Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => rows.WriteToServerAsync(x, table, cancellation.Token));

                await act.Should().ThrowAsync<OperationCanceledException>();
            }

            CountRows(table).Should().Be(0);
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
        [Trait("Fixed", "Q129")]
        public void WriteToServer_FieldNotInSource_ThrowsIndexOutOfRangeException()
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(ContextKind.Connection, x => NamedRows.WriteToServer(x, table, "ID", "Missing"));

            act.Should().Throw<IndexOutOfRangeException>().WithMessage("Field 'Missing' is not found");
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

        // Npgsql 10 reads date as DateOnly, which binary COPY writes as date; Npgsql 4 read it as DateTime and wrote a
        // timestamp, which the server rejected ("22P03: incorrect binary data format", Q85)
        [Fact]
        [Trait("UpgradeSensitive", "Npgsql binary COPY type inference")]
        public void WriteToServer_DateColumnFromQuery_CopiesValues()
        {
            var table = Database.CreateScratchTable("\"ID\" int not null, \"DateOfFoundation\" date not null");

            Database.Run(ContextKind.Connection, x => SourceContext()
                .CreateSimple("select \"ID\", \"DateOfFoundation\" from \"Company\"").WriteToServer(x, table));

            CountRows(table).Should().Be(2);
            Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from \"Company\" c join {table} t "
                    + "on t.\"ID\" = c.\"ID\" and t.\"DateOfFoundation\" = c.\"DateOfFoundation\"")
                .ExecuteQueryFirstColumn<long>().Single()).Should().Be(2);
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

        // every row takes two seconds: a trigger sleeps
        private string CreateSlowTable()
        {
            var table = CreateNamedTable();
            Database.Execute($"create function {table}_slow() returns trigger language plpgsql as $$ begin perform pg_sleep(2); return new; end $$; "
                + $"create trigger {table}_slow before insert on {table} for each row execute function {table}_slow();");
            return table;
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
