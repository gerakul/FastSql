using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.PostgreSQL;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    // P1 (phase 5): table creation by a PostgreSQL bulk copy (CreateTable with ColumnDefinitionOptions), the scenarios
    // of the SQL Server tests where PostgreSQL has them. Q10 is fixed: CreateTable threw NotImplementedException
    [Trait("Provider", "PostgreSQL")]
    [Trait("Fixed", "Q10")]
    public class PostgreSqlCreateTableTests : IClassFixture<PostgreSqlDatabase>
    {
        public PostgreSqlCreateTableTests(PostgreSqlDatabase database)
        {
            this.Database = database;
        }

        private PostgreSqlDatabase Database { get; }

        #region From a query

        // Npgsql reports the types without their sizes and the sizes apart; nullability and keys only with KeyInfo, so
        // every column is nullable and there is no primary key (the source has one)
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "Npgsql column schema")]
        public void WriteToServer_CreateTableFromQuery_ReproducesColumnTypes(ContextKind kind)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(kind, x => SourceContext().CreateSimple($"select * from {source}").WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true)));

            DescribeColumns(table).Should().Equal(TypedSourceColumns);
            ReadPrimaryKey(table).Should().BeEmpty();
            CountRows(table).Should().Be(2);
            CountDifferentRows(source, table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "Npgsql column schema")]
        public async Task WriteToServerAsync_CreateTableFromQuery_ReproducesColumnTypesAsync(ContextKind kind)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServerAsync(x, table, new NpgsqlBulkOptions(createTable: true), TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(TypedSourceColumns);
            CountRows(table).Should().Be(2);
            CountDifferentRows(source, table).Should().Be(0);
        }

        [Fact]
        public void WriteToServer_CreateTableFromQueryWithPrimaryKeyOption_CreatesPrimaryKey()
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: ColumnDefinitionOptions.FromDefault("ID"))));

            ReadPrimaryKey(table).Should().Equal("ID");
        }

        // with KeyInfo, Npgsql reports the key and the nullability, so the table gets both (unlike SQL Server, Q103)
        [Fact]
        [Trait("UpgradeSensitive", "Npgsql column schema")]
        public void WriteToServer_CreateTableFromKeyInfoReader_ReproducesPrimaryKeyAndNullability()
        {
            var source = Database.CreateScratchTable("\"ID\" int not null primary key, \"Name\" varchar(50) null, \"Age\" int not null");
            Database.Execute($"insert into {source} values (1, 'one', 30), (2, null, 40)");
            var table = NewTableName();

            using (var connection = (NpgsqlConnection)Database.OpenConnection())
            using (var command = new NpgsqlCommand($"select * from {source}", connection))
            using (var reader = command.ExecuteReader(System.Data.CommandBehavior.KeyInfo))
            {
                Database.Run(ContextKind.Connection, x => reader.WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true)));
            }

            DescribeColumns(table).Should().Equal("ID integer not null", "Name character varying(50) null", "Age integer not null");
            ReadPrimaryKey(table).Should().Equal("ID");
            CountRows(table).Should().Be(2);
        }

        // the reader reports the non-nullable CLR type for a nullable column, so the column is created as not null (Q104,
        // as on SQL Server)
        [Fact]
        [Trait("Quirk", "Q104")]
        public void WriteToServer_CreateTableFromQueryIgnoringSchemaTable_CreatesValueTypeColumnsAsNotNull()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext()
                .CreateSimple("select \"CompanyID\", \"Name\", \"Age\" from \"Employee\" order by \"ID\"")
                .WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            act.Should().Throw<PostgresException>().WithMessage($"23502: null value in column \"Age\" of relation \"{table}\" violates not-null constraint*");
            DescribeColumns(table).Should().Equal("CompanyID integer not null", "Name character varying(4000) null", "Age integer not null");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        public void WriteToServer_CreateTableWithFields_CreatesOnlyListedColumns()
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true), "ID", "Name"));

            DescribeColumns(table).Should().Equal("ID integer null", "Name character varying(50) null");
            CountRows(table).Should().Be(2);
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("name")]
        [Trait("Quirk", "Q130")]
        public void WriteToServer_CreateTableWithFieldNotInSource_ThrowsInvalidOperationException(string field)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true), "ID", field));

            act.Should().Throw<InvalidOperationException>().WithMessage($"Cannot create table {table}: the source has no field '{field}'*");
            TableExists(table).Should().BeFalse();
        }

        #endregion

        #region From an enumerable

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableFromEnumerable_CreatesColumnsFromMemberTypes(ContextKind kind)
        {
            var table = NewTableName();

            // the provider-independent BulkOptions work too
            Database.Run(kind, x => TypedRows.WriteToServer(x, table, new BulkOptions(createTable: true)));

            DescribeColumns(table).Should().Equal(TypedRowColumns);
            ReadPrimaryKey(table).Should().BeEmpty();
            ReadAll<TypedRow>(table).Should().BeEquivalentTo(TypedRows, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableFromEnumerable_CreatesColumnsFromMemberTypesAsync(ContextKind kind)
        {
            var table = NewTableName();

            await Database.RunAsync(kind, x => TypedRows.WriteToServerAsync(x, table, new NpgsqlBulkOptions(createTable: true),
                TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(TypedRowColumns);
            ReadAll<TypedRow>(table).Should().BeEquivalentTo(TypedRows, o => o.WithStrictOrdering());
        }

        // a DateTimeOffset member gets timestamptz; binary COPY writes offset 0 only
        [Fact]
        public void WriteToServer_CreateTableFromEnumerableWithDateTimeOffset_CreatesTimestamptz()
        {
            var table = NewTableName();
            var rows = new[] { new MomentRow() { ID = 1, Moment = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero) }, new MomentRow() { ID = 2 } };

            Database.Run(ContextKind.Connection, x => rows.WriteToServer(x, table, new BulkOptions(createTable: true)));

            DescribeColumns(table).Should().Equal("ID integer not null", "Moment timestamp with time zone(6) null");
            Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table} where \"ID\" = 1 and \"Moment\" = '2020-01-02 03:04:05+00'")
                .ExecuteQueryFirstColumn<long>().Single()).Should().Be(1);
        }

        public static TheoryData<string, string[]> ColumnDefinitionOptionsCases => new TheoryData<string, string[]>()
        {
            { "Default", new[] { "ID integer not null", "Name character varying(4000) null", "Data bytea null", "Amount numeric(38,18) not null", "Created timestamp without time zone(6) not null" } },
            { "Max", new[] { "ID integer not null", "Name text null", "Data bytea null", "Amount numeric(38,18) not null", "Created timestamp without time zone(6) not null" } },
            { "Small", new[] { "ID integer not null", "Name character varying(255) null", "Data bytea null", "Amount numeric(19,9) not null", "Created timestamp without time zone(3) not null" } }
        };

        [Theory]
        [MemberData(nameof(ColumnDefinitionOptionsCases))]
        public void WriteToServer_CreateTableWithColumnDefinitionOptions_UsesSizesAndPrimaryKey(string options, string[] expectedColumns)
        {
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table,
                new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: WithPrimaryKey(options, "ID"))));

            DescribeColumns(table).Should().Equal(expectedColumns);
            ReadPrimaryKey(table).Should().Equal("ID");
            ReadAll<SizedRow>(table).Should().BeEquivalentTo(SizedRows, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ColumnDefinitionOptionsCases))]
        public async Task WriteToServerAsync_CreateTableWithColumnDefinitionOptions_UsesSizesAndPrimaryKeyAsync(string options, string[] expectedColumns)
        {
            var table = NewTableName();

            await Database.RunAsync(ContextKind.Connection, x => SizedRows.WriteToServerAsync(x, table,
                new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: WithPrimaryKey(options, "ID")),
                TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(expectedColumns);
            ReadPrimaryKey(table).Should().Equal("ID");
        }

        [Fact]
        public void WriteToServer_CreateTableWithUnsupportedMemberType_ThrowsInvalidOperationException()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true)));

            act.Should().Throw<InvalidOperationException>().WithMessage($"Cannot create table {table}: there is no column type for the field 'Duration' (Version)");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public async Task WriteToServerAsync_CreateTableWithUnsupportedMemberType_ThrowsInvalidOperationExceptionAsync()
        {
            var table = NewTableName();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => UnsupportedRows.WriteToServerAsync(x, table,
                new NpgsqlBulkOptions(createTable: true), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Cannot create table *");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public void WriteToServer_CreateTableWithUnsupportedMemberTypeNotInFields_CreatesTableWithSelectedFields()
        {
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table, new NpgsqlBulkOptions(createTable: true), "ID"));

            DescribeColumns(table).Should().Equal("ID integer not null");
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public void WriteToServer_CreateTableWithUnsupportedMemberTypeAndThrowIfUnsupportedType_ThrowsArgumentOutOfRangeException()
        {
            var table = NewTableName();
            var columnOptions = ColumnDefinitionOptions.FromDefault();
            columnOptions.ThrowIfUnsupportedType = true;

            Action act = () => Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table,
                new NpgsqlBulkOptions(createTable: true, columnDefinitionOptions: columnOptions)));

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Unsupported type*").WithParameterName("type")
                .Which.ActualValue.Should().Be(typeof(Version));
            TableExists(table).Should().BeFalse();
        }

        #endregion

        #region Existing tables, temporary tables, transactions and option levels

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableWhenTableExists_ThrowsPostgresException(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            Action act = () => Database.Run(kind, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            act.Should().Throw<PostgresException>().WithMessage($"42P07: relation \"{table}\" already exists*");
            CountRows(table).Should().Be(SizedRows.Length);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableWhenTableExists_ThrowsPostgresExceptionAsync(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            Func<Task> act = () => Database.RunAsync(kind, x => SizedRows.WriteToServerAsync(x, table, CreateFromMembers(checkIfNotExists: false),
                TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<PostgresException>().WithMessage($"42P07: relation \"{table}\" already exists*");
            CountRows(table).Should().Be(SizedRows.Length);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableWhenTableExistsAndCheckIfNotExists_AppendsRows(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            Database.Run(kind, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            CountRows(table).Should().Be(SizedRows.Length * 2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableWhenTableExistsAndCheckIfNotExists_AppendsRowsAsync(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            await Database.RunAsync(kind, x => SizedRows.WriteToServerAsync(x, table, CreateFromMembers(checkIfNotExists: true),
                TestContext.Current.CancellationToken));

            CountRows(table).Should().Be(SizedRows.Length * 2);
        }

        // a table created in the pg_temp schema is a temporary table of the session
        [Fact]
        public void WriteToServer_CreateTableIfNotExistsTwiceIntoTempTable_CopiesBothTimes()
        {
            var options = new NpgsqlBulkOptions(createTable: true, checkTableIfNotExistsBeforeCreation: true);

            var (count, persistence) = Database.Run(ContextKind.Connection, x =>
            {
                SizedRows.WriteToServer(x, "pg_temp.bulk_temp", options);
                SizedRows.WriteToServer(x, "pg_temp.bulk_temp", options);
                return (x.CreateSimple("select count(*) from pg_temp.bulk_temp").ExecuteQueryFirstColumn<long>().Single(),
                    x.CreateSimple("select relpersistence::text from pg_class where oid = 'pg_temp.bulk_temp'::regclass").ExecuteQueryFirstColumn<string>().Single());
            });

            count.Should().Be(SizedRows.Length * 2);
            persistence.Should().Be("t");
        }

        // table creation is transactional in PostgreSQL
        [Fact]
        public void WriteToServer_CreateTableInRolledBackTransaction_DiscardsTable()
        {
            var table = NewTableName();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                SizedRows.WriteToServer(Database.Spec.DefaultProvider.CreateContext(transaction), table, CreateFromMembers(checkIfNotExists: false));
                transaction.Rollback();
            }

            TableExists(table).Should().BeFalse();
        }

        // the property of NpgsqlBulkOptions, without the constructor
        [Fact]
        public void WriteToServer_CreateTableSetOnNpgsqlBulkOptions_CreatesTable()
        {
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple("select \"ID\", \"Name\" from \"Company\"")
                .WriteToServer(x, table, new NpgsqlBulkOptions() { CreateTable = true }));

            DescribeColumns(table).Should().Equal("ID integer null", "Name character varying(50) null");
            CountRows(table).Should().Be(2);
        }

        [Fact]
        public async Task WriteToServerAsync_CreateTableAtProviderLevel_CreatesTableAsync()
        {
            var table = NewTableName();
            var provider = Database.Spec.CreateProvider();
            provider.BulkOptions.CreateTable = true;
            ((NpgsqlBulkOptions)provider.BulkOptions).ColumnDefinitionOptions = ColumnDefinitionOptions.FromSmall("ID");

            await Database.RunAsync(provider, ContextKind.Connection, x => SizedRows.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal("ID integer not null", "Name character varying(255) null", "Data bytea null", "Amount numeric(19,9) not null",
                "Created timestamp without time zone(3) not null");
            ReadPrimaryKey(table).Should().Equal("ID");
        }

        #endregion

        #region Helpers

        private static NpgsqlBulkOptions CreateFromMembers(bool checkIfNotExists)
        {
            return new NpgsqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, checkTableIfNotExistsBeforeCreation: checkIfNotExists);
        }

        // From* clone the static instances, which must not be changed
        private static ColumnDefinitionOptions WithPrimaryKey(string options, params string[] primaryKey)
        {
            switch (options)
            {
                case "Default":
                    return ColumnDefinitionOptions.FromDefault(primaryKey);
                case "Max":
                    return ColumnDefinitionOptions.FromMax(primaryKey);
                case "Small":
                    return ColumnDefinitionOptions.FromSmall(primaryKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options, "Unknown options");
            }
        }

        private static string NewTableName()
        {
            return "bulk_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private DbContext SourceContext()
        {
            return Database.Spec.DefaultProvider.CreateContext(Database.ConnectionString);
        }

        private string CreateTypedSourceTable()
        {
            var table = Database.CreateScratchTable("\"ID\" int not null primary key, \"Code\" char(5) not null, \"Name\" varchar(50) not null, "
                + "\"Note\" varchar(20) null, \"Body\" text null, \"Amount\" numeric(10, 3) null, \"Created\" timestamp(3) not null, \"Legacy\" timestamp null, "
                + "\"Duration\" time(2) null, \"Moment\" timestamptz(5) null, \"Data\" bytea null, \"Flag\" boolean not null, \"Ratio\" double precision null, "
                + "\"Tags\" integer[] null, \"Plain\" numeric null");
            Database.Execute($"insert into {table} values "
                + "(1, 'AB', 'first', 'note', 'long text', 12.345, '2020-01-02 03:04:05.678', '2001-02-03 04:05:06', '01:02:03.45', "
                + "'2020-01-02 03:04:05.12345+02', '\\x0102', true, 1.5, '{1,2,3}', 7.25), "
                + "(2, 'CD', 'second', null, null, null, '2021-06-07 08:09:10', null, null, null, null, false, null, null, null)");
            return table;
        }

        private static string[] TypedSourceColumns => new[]
        {
            "ID integer null",
            "Code character(5) null",
            "Name character varying(50) null",
            "Note character varying(20) null",
            "Body text null",
            "Amount numeric(10,3) null",
            "Created timestamp without time zone(3) null",
            "Legacy timestamp without time zone(6) null",
            "Duration time without time zone(2) null",
            "Moment timestamp with time zone(5) null",
            "Data bytea null",
            "Flag boolean null",
            "Ratio double precision null",
            "Tags _int4 null",
            "Plain numeric null"
        };

        private static string[] TypedRowColumns => new[]
        {
            "ID integer not null",
            "Number integer null",
            "Big bigint not null",
            "Small smallint not null",
            "Tiny smallint not null",
            "Flag boolean not null",
            "MaybeFlag boolean null",
            "Amount numeric(38,18) not null",
            "Ratio double precision not null",
            "Single real not null",
            "Created timestamp without time zone(6) not null",
            "Modified timestamp without time zone(6) null",
            "Day date not null",
            "At time without time zone(6) not null",
            "Duration interval(6) not null",
            "Uid uuid not null",
            "Name character varying(4000) null",
            "Data bytea null"
        };

        // column name, type with size and nullability, from information_schema
        private string[] DescribeColumns(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select column_name as \"Name\", data_type as \"DataType\", udt_name as \"UdtName\", "
                    + "character_maximum_length as \"MaxLength\", numeric_precision as \"Precision\", numeric_scale as \"Scale\", "
                    + "datetime_precision as \"DateTimePrecision\", is_nullable as \"IsNullable\" "
                    + "from information_schema.columns where table_name = @p0 order by ordinal_position", table)
                .ExecuteQuery<ColumnInfo>().Select(Describe).ToArray());
        }

        private static string Describe(ColumnInfo column)
        {
            string type;
            if (column.DataType == "ARRAY")
            {
                type = column.UdtName;
            }
            else if (column.DataType == "numeric")
            {
                type = column.Precision.HasValue ? $"numeric({column.Precision},{column.Scale})" : "numeric";
            }
            else if (column.DataType.StartsWith("timestamp", StringComparison.Ordinal) || column.DataType.StartsWith("time ", StringComparison.Ordinal)
                || column.DataType == "interval")
            {
                type = $"{column.DataType}({column.DateTimePrecision})";
            }
            else
            {
                type = column.MaxLength.HasValue ? $"{column.DataType}({column.MaxLength})" : column.DataType;
            }

            return $"{column.Name} {type} {(column.IsNullable == "YES" ? "null" : "not null")}";
        }

        private string[] ReadPrimaryKey(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select k.column_name::text from information_schema.table_constraints c "
                    + "join information_schema.key_column_usage k on k.constraint_name = c.constraint_name and k.table_name = c.table_name "
                    + "where c.table_name = @p0 and c.constraint_type = 'PRIMARY KEY' order by k.ordinal_position", table)
                .ExecuteQueryFirstColumn<string>().ToArray());
        }

        private bool TableExists(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select count(*) from information_schema.tables where table_name = @p0", table)
                .ExecuteQueryFirstColumn<long>().Single()) == 1;
        }

        private long CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<long>().Single());
        }

        // rows of the first table missing from the second one
        private long CountDifferentRows(string first, string second)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from (select * from {first} except select * from {second}) d")
                .ExecuteQueryFirstColumn<long>().Single());
        }

        private T[] ReadAll<T>(string table) where T : new()
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by \"ID\"").ExecuteQuery<T>().ToArray());
        }

        #endregion

        #region Models

        private static TypedRow[] TypedRows => new[]
        {
            new TypedRow()
            {
                ID = 1, Number = 42, Big = 5000000000L, Small = -7, Tiny = 200, Flag = true, MaybeFlag = false, Amount = 12.5m, Ratio = 0.25,
                Single = 1.5f, Created = new DateTime(2020, 1, 2, 3, 4, 5), Modified = new DateTime(2021, 2, 3), Day = new DateOnly(2020, 1, 2),
                At = new TimeOnly(10, 11, 12), Duration = new TimeSpan(1, 2, 3, 4), Uid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
                Name = "first", Data = new byte[] { 1, 2, 3 }
            },
            new TypedRow()
            {
                ID = 2, Number = null, Big = 0, Small = 0, Tiny = 0, Flag = false, MaybeFlag = null, Amount = 0m, Ratio = 0, Single = 0,
                Created = new DateTime(2022, 3, 4), Modified = null, Day = new DateOnly(2022, 3, 4), At = TimeOnly.MinValue, Duration = TimeSpan.Zero,
                Uid = Guid.Empty, Name = null, Data = null
            }
        };

        private static SizedRow[] SizedRows => new[]
        {
            new SizedRow() { ID = 1, Name = "first", Data = new byte[] { 1, 2 }, Amount = 1.25m, Created = new DateTime(2020, 1, 2, 3, 4, 5) },
            new SizedRow() { ID = 2, Name = null, Data = null, Amount = 0m, Created = new DateTime(2021, 6, 7) }
        };

        private static UnsupportedRow[] UnsupportedRows => new[]
        {
            new UnsupportedRow() { ID = 1, Duration = new Version(1, 2) }
        };

        // one member of each type that PostgreSQL table creation supports, except DateTimeOffset (see MomentRow)
        private class TypedRow
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
            public DateTime? Modified { get; set; }
            public DateOnly Day { get; set; }
            public TimeOnly At { get; set; }
            public TimeSpan Duration { get; set; }
            public Guid Uid { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }

        private class MomentRow
        {
            public int ID { get; set; }
            public DateTimeOffset? Moment { get; set; }
        }

        // members whose column sizes depend on ColumnDefinitionOptions
        private class SizedRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
            public decimal Amount { get; set; }
            public DateTime Created { get; set; }
        }

        private class UnsupportedRow
        {
            public int ID { get; set; }
            public Version Duration { get; set; }
        }

        private class ColumnInfo
        {
            public string Name { get; set; }
            public string DataType { get; set; }
            public string UdtName { get; set; }
            public int? MaxLength { get; set; }
            public int? Precision { get; set; }
            public int? Scale { get; set; }
            public int? DateTimePrecision { get; set; }
            public string IsNullable { get; set; }
        }

        #endregion
    }
}
