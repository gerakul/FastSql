using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;
using NpgsqlColumnDefinitionOptions = Gerakul.FastSql.PostgreSQL.ColumnDefinitionOptions;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    // P1 (phase 5): the table a PostgreSQL bulk copy creates. The script is the first command of the copy, so a context
    // over a connection that is never opened captures it; running it then fails, which these tests ignore
    [Trait("Fixed", "Q10")]
    public class NpgsqlCreateTableTests
    {
        public class TypedRow
        {
            public int ID { get; set; }
            public int? Number { get; set; }
            public long Big { get; set; }
            public short Small { get; set; }
            public byte Tiny { get; set; }
            public bool Flag { get; set; }
            public bool? MaybeFlag { get; set; }
            public decimal Amount { get; set; }
            public double Ratio { get; set; }
            public float Single { get; set; }
            public DateTime Created { get; set; }
            public DateTimeOffset Moment { get; set; }
            public DateOnly Day { get; set; }
            public TimeOnly At { get; set; }
            public TimeSpan Duration { get; set; }
            public Guid Uid { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }

        public class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        public class UnsupportedRow
        {
            public int ID { get; set; }
            public Version Version { get; set; }
        }

        private static readonly TypedRow[] typedRows = { new TypedRow() };
        private static readonly Person[] persons = { new Person() { ID = 1, Name = "Alice" } };

        private static string CaptureScript(DbDataReader reader, BulkOptions options, params string[] fields)
        {
            var context = new CapturingNpgsqlConnectionContext(NpgsqlContextProvider.DefaultInstance);

            try
            {
                reader.WriteToServer(context, "new_table", options, fields);
            }
            catch (InvalidOperationException) when (context.Commands.Count > 0)
            {
                // the connection was never opened
            }

            return context.Commands[0].CommandText;
        }

        private static string[] Columns(string script)
        {
            var body = script.Substring(script.IndexOf('(') + 1, script.LastIndexOf(')') - script.IndexOf('(') - 1);
            return body.Split(new[] { Environment.NewLine + "\t," }, StringSplitOptions.None).Select(x => x.Trim()).ToArray();
        }

        [Fact]
        public void FromMemberTypes_DeclaresColumnsThatBinaryCopyFits()
        {
            var script = CaptureScript(typedRows.ToDataReader(), new BulkOptions(createTable: true));

            script.Trim().Should().StartWith("create table new_table" + Environment.NewLine + "(");
            Columns(script).Should().Equal(
                "\"ID\" integer not null",
                "\"Number\" integer null",
                "\"Big\" bigint not null",
                "\"Small\" smallint not null",
                "\"Tiny\" smallint not null",
                "\"Flag\" boolean not null",
                "\"MaybeFlag\" boolean null",
                "\"Amount\" numeric(38, 18) not null",
                "\"Ratio\" double precision not null",
                "\"Single\" real not null",
                "\"Created\" timestamp(6) not null",
                "\"Moment\" timestamptz(6) not null",
                "\"Day\" date not null",
                "\"At\" time(6) not null",
                "\"Duration\" interval(6) not null",
                "\"Uid\" uuid not null",
                "\"Name\" varchar(4000) null",
                "\"Data\" bytea null");
        }

        public static TheoryData<string, string[]> PresetCases => new TheoryData<string, string[]>()
        {
            { "Default", new[] { "\"ID\" integer primary key not null", "\"Amount\" numeric(38, 18) not null", "\"Created\" timestamp(6) not null", "\"Name\" varchar(4000) null", "\"Data\" bytea null" } },
            { "Max", new[] { "\"ID\" integer primary key not null", "\"Amount\" numeric(38, 18) not null", "\"Created\" timestamp(6) not null", "\"Name\" text null", "\"Data\" bytea null" } },
            { "Small", new[] { "\"ID\" integer primary key not null", "\"Amount\" numeric(19, 9) not null", "\"Created\" timestamp(3) not null", "\"Name\" varchar(255) null", "\"Data\" bytea null" } }
        };

        // the columns keep the order of the source, not of the field list, as on SQL Server
        [Theory]
        [MemberData(nameof(PresetCases))]
        public void WithPreset_UsesItsSizesAndPrimaryKey(string preset, string[] expected)
        {
            var columnOptions = preset switch
            {
                "Default" => NpgsqlColumnDefinitionOptions.FromDefault("ID"),
                "Max" => NpgsqlColumnDefinitionOptions.FromMax("ID"),
                _ => NpgsqlColumnDefinitionOptions.FromSmall("ID")
            };

            var script = CaptureScript(typedRows.ToDataReader(), new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: columnOptions),
                "ID", "Name", "Amount", "Created", "Data");

            Columns(script).Should().Equal(expected);
        }

        [Fact]
        public void DateTimeScaleAboveSix_IsTakenAsSix()
        {
            var columnOptions = NpgsqlColumnDefinitionOptions.FromDefault();
            columnOptions.DateTimeScale = 7;

            var script = CaptureScript(typedRows.ToDataReader(), new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: columnOptions),
                "Created", "Moment", "At", "Duration");

            Columns(script).Should().Equal("\"Created\" timestamp(6) not null", "\"Moment\" timestamptz(6) not null", "\"At\" time(6) not null",
                "\"Duration\" interval(6) not null");
        }

        [Fact]
        public void CheckTableIfNotExists_UsesIfNotExists()
        {
            var script = CaptureScript(persons.ToDataReader(), new BulkOptions(createTable: true, checkTableIfNotExistsBeforeCreation: true));

            script.Trim().Should().StartWith("create table if not exists new_table");
        }

        // a reader that Npgsql did not make has its columns described by the field types; the names keep their case and
        // a double quote in a name is written twice
        [Fact]
        public void FromDataTableReader_UsesFieldTypesAndQuotesNames()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("say \"hi\"", typeof(string));
            table.Rows.Add(1, "hi");

            var script = CaptureScript(table.CreateDataReader(), new BulkOptions(createTable: true));

            Columns(script).Should().Equal("\"Id\" integer not null", "\"say \"\"hi\"\"\" varchar(4000) null");
        }

        [Fact]
        public void UnsupportedMemberType_NamesTheField()
        {
            Action act = () => CaptureScript(new[] { new UnsupportedRow() }.ToDataReader(), new BulkOptions(createTable: true));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot create table new_table: there is no column type for the field 'Version' (Version)");
        }

        [Fact]
        public void UnsupportedMemberTypeNotInFields_IsLeftOut()
        {
            var script = CaptureScript(new[] { new UnsupportedRow() }.ToDataReader(), new BulkOptions(createTable: true), "ID");

            Columns(script).Should().Equal("\"ID\" integer not null");
        }

        [Fact]
        public void UnsupportedMemberTypeAndThrowIfUnsupportedType_ThrowsArgumentOutOfRangeException()
        {
            var columnOptions = NpgsqlColumnDefinitionOptions.FromDefault();
            columnOptions.ThrowIfUnsupportedType = true;

            Action act = () => CaptureScript(new[] { new UnsupportedRow() }.ToDataReader(),
                new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: columnOptions));

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Unsupported type*").WithParameterName("type")
                .Which.ActualValue.Should().Be(typeof(Version));
        }

        // the messages of SQL Server (phase 4, I8), from the same code
        [Theory]
        [InlineData(new[] { "ID", "Missing" }, "the source has no field 'Missing'")]
        [InlineData(new[] { "id", "Name" }, "the source has no field 'id' (it has 'ID': the field list is compared with case)")]
        [InlineData(new[] { "ID", "Name", "ID" }, "the field list names 'ID' more than once")]
        [InlineData(new[] { "ID", "ID", "Missing" }, "the source has no field 'Missing'; the field list names 'ID' more than once")]
        [Trait("Quirk", "Q130")]
        public void FieldListProblems_AreNamed(string[] fields, string problems)
        {
            Action act = () => CaptureScript(persons.ToDataReader(), new BulkOptions(createTable: true), fields);

            act.Should().Throw<InvalidOperationException>().WithMessage("Cannot create table new_table: " + problems);
        }
    }
}
