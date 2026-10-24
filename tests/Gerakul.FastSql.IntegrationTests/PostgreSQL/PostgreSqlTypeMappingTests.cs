using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    // I6 for PostgreSQL: values written through Insert (parameters) and bulk copy (binary COPY), read back through every
    // read path. Everything here depends on the driver, Npgsql (UpgradeSensitive).
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlTypeMappingTests : TypeMappingTests<PostgreSqlDatabase>
    {
        private const string Driver = "Npgsql: CLR types returned and values written";
        private const string BinaryCopy = "Npgsql binary COPY infers the PostgreSQL type from the CLR type";
        private const string TextParameter = "Npgsql sends string parameters as text";
        private const string Timestamptz = "Npgsql 6+ reads timestamptz as a UTC DateTime and writes a non-UTC DateTime as timestamp (Npgsql 4 read a local DateTime)";
        private const string UnmappedEnum = "Npgsql handling of unmapped enums";
        private const string DateAndTime = "Npgsql 10 returns DateOnly and TimeOnly for date and time (Npgsql 4 returned DateTime and TimeSpan)";

        private static readonly Guid sampleGuid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        private static readonly TypeMappingWritePath[] bulkCopyOnly = new[] { TypeMappingWritePath.BulkCopy };

        // values that both write paths store unchanged (or change the same way); the differences are in the edge tests below
        private static readonly TypeCase[] cases = new TypeCase[]
        {
            new TypeCase<bool, bool?>("boolean", new[] { false, true }),
            new TypeCase<short, short?>("smallint", new[] { short.MinValue, (short)0, short.MaxValue }),
            new TypeCase<int, int?>("integer", new[] { int.MinValue, 0, int.MaxValue }),
            new TypeCase<long, long?>("bigint", new[] { long.MinValue, 0L, long.MaxValue }),
            new TypeCase<decimal, decimal?>("numeric", new[] { decimal.MinValue, 0.0000000000000000000000000001m, decimal.MaxValue }),
            new TypeCase<float, float?>("real", new[] { float.MinValue, 0.1f, float.MaxValue }),
            new TypeCase<double, double?>("double precision",
                new[] { double.MinValue, 0.1, double.MaxValue, double.NaN, double.NegativeInfinity, double.PositiveInfinity }),
            // Npgsql 10 returns DateOnly and TimeOnly for date and time, and binary COPY writes them as date and time.
            // Npgsql 4 returned DateTime and TimeSpan, which binary COPY wrote as timestamp and interval, so these cases
            // were Insert only (Q85). DateTime and TimeSpan members: see "Date and time".
            new TypeCase<DateOnly, DateOnly?>("date", new[] { DateOnly.MinValue, new DateOnly(2020, 2, 29), DateOnly.MaxValue }),
            new TypeCase<TimeOnly, TimeOnly?>("time",
                new[] { TimeOnly.MinValue, new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(1234560)), new TimeOnly(23, 59, 59, 999, 999) }),
            // timestamp keeps microseconds; MinValue and MaxValue are written as -infinity and infinity and read back as is
            // (Npgsql 6+; Npgsql 4 wrote MaxValue as a timestamp and read it back truncated to microseconds)
            new TypeCase<DateTime, DateTime?>("timestamp",
                new[] { DateTime.MinValue, new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(1234560), DateTime.MaxValue }),
            new TypeCase<TimeSpan, TimeSpan?>("interval",
                new[] { TimeSpan.FromTicks(-10), TimeSpan.Zero, new TimeSpan(3, 13, 14, 15).Add(TimeSpan.FromTicks(1234560)), TimeSpan.FromDays(400) }),
            new TypeCase<Guid, Guid?>("uuid", new[] { Guid.Empty, sampleGuid }),
            new TypeCase<string, string>("char(5)", new[] { "ab", "", "Grüße" }, expected: new[] { "ab   ", "     ", "Grüße" }),
            new TypeCase<string, string>("varchar(20)", new[] { "", "abc", "Привет 😀" }),
            new TypeCase<string, string>("text", new[] { "", "Привет 😀", new string('x', 10000) }),
            new TypeCase<byte[], byte[]>("bytea", new[] { new byte[0], new byte[] { 1, 2, 3 }, Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray() }),
            // bulk copy only: Insert sends text, which json does not accept (Q86); the text is stored as is
            new TypeCase<string, string>("json", new[] { "{\"b\": 1,  \"a\": [1, 2]}", "[]" }, writePaths: bulkCopyOnly)
        };

        public PostgreSqlTypeMappingTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        public static TheoryData<string, TypeMappingWritePath> Cases => RoundTripData(cases);

        public static TheoryData<TypeMappingWritePath> WritePaths => new TheoryData<TypeMappingWritePath>()
        {
            TypeMappingWritePath.Insert,
            TypeMappingWritePath.BulkCopy
        };

        #region Round trip of every type

        [Theory]
        [MemberData(nameof(Cases))]
        [Trait("UpgradeSensitive", Driver)]
        public void ExecuteQuery_AfterWrite_ReturnsWrittenValues(string columnType, TypeMappingWritePath writePath)
        {
            var typeCase = FindCase(cases, columnType);
            var table = WriteRows(typeCase, writePath);

            typeCase.AssertExecuteQuery(CreateContext(), Spec, table);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        [Trait("UpgradeSensitive", Driver)]
        public void ExecuteQueryAnonymous_AfterWrite_ReturnsWrittenValues(string columnType, TypeMappingWritePath writePath)
        {
            var typeCase = FindCase(cases, columnType);
            var table = WriteRows(typeCase, writePath);

            typeCase.AssertExecuteQueryAnonymous(CreateContext(), Spec, table);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        [Trait("UpgradeSensitive", Driver)]
        public void ExecuteQueryFirstColumn_AfterWrite_ReturnsWrittenValues(string columnType, TypeMappingWritePath writePath)
        {
            var typeCase = FindCase(cases, columnType);
            var table = WriteRows(typeCase, writePath);

            typeCase.AssertExecuteQueryFirstColumn(CreateContext(), Spec, table);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        [Trait("UpgradeSensitive", Driver)]
        public void ExecuteQueryUntyped_AfterWrite_ReturnsDriverClrTypes(string columnType, TypeMappingWritePath writePath)
        {
            var typeCase = FindCase(cases, columnType);
            var table = WriteRows(typeCase, writePath);

            typeCase.AssertExecuteQueryUntyped(CreateContext(), Spec, table);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        [Trait("UpgradeSensitive", Driver)]
        public async Task ReadAsync_AfterWriteAsync_ReturnsWrittenValuesAsync(string columnType, TypeMappingWritePath writePath)
        {
            var typeCase = FindCase(cases, columnType);
            var table = await WriteRowsAsync(typeCase, writePath);

            await typeCase.AssertReadAsync(CreateContext(), Spec, table);
        }

        #endregion

        #region Binary COPY type inference

        // the binary COPY data has the format of the type Npgsql infers from the CLR value, the server reads it as the column type
        public static TheoryData<string, object, string> MismatchedBulkCopyValues => new TheoryData<string, object, string>()
        {
            { "smallint", 5, "22P03: incorrect binary data format" },
            { "integer", 5L, "22P03: incorrect binary data format" },
            { "bigint", 5, "08P01: insufficient data left in message" },
            { "real", 0.1, "22P03: incorrect binary data format" },
            { "double precision", 0.1f, "08P01: insufficient data left in message" },
            { "date", new DateTime(2020, 2, 29), "22P03: incorrect binary data format" },
            { "time", new TimeSpan(13, 14, 15), "22P03: incorrect binary data format" }
        };

        // Insert sends the inferred type too, but the server casts the parameter to the column type
        public static TheoryData<string, object, object> MismatchedInsertValues => new TheoryData<string, object, object>()
        {
            { "smallint", 5, (short)5 },
            { "integer", 5L, 5 },
            { "bigint", 5, 5L },
            { "real", 0.1, 0.1f },
            { "double precision", 0.1f, (double)0.1f },
            { "date", new DateTime(2020, 2, 29, 13, 14, 15), new DateOnly(2020, 2, 29) },
            { "time", new TimeSpan(1, 13, 14, 15), new TimeOnly(13, 14, 15) },
            { "time", TimeSpan.FromTicks(-10), new TimeOnly(23, 59, 59, 999, 999) }
        };

        [Theory]
        [MemberData(nameof(MismatchedBulkCopyValues))]
        [Trait("Quirk", "Q85")]
        [Trait("UpgradeSensitive", BinaryCopy)]
        public void BulkCopy_ValueOfOtherTypeThanColumn_ThrowsPostgresException<T>(string columnType, T value, string message)
        {
            var table = CreateValueTable(columnType);

            Action act = () => WriteValue(table, TypeMappingWritePath.BulkCopy, value);

            act.Should().ThrowExactly<PostgresException>().WithMessage(message);
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(MismatchedInsertValues))]
        [Trait("Quirk", "Q85")]
        [Trait("UpgradeSensitive", Driver)]
        public void Insert_ValueOfOtherTypeThanColumn_IsCastByServer<T>(string columnType, T value, object expected)
        {
            var table = CreateValueTable(columnType);

            WriteValue(table, TypeMappingWritePath.Insert, value);

            ReadRawValue(table).Should().Be(expected);
        }

        #endregion

        #region Date and time

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DateTimeWithTicksIntoTimestamp_TruncatesToMicroseconds(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("timestamp");

            WriteValue(table, writePath, new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(1234567));

            ReadValue<DateTime>(table).Should().Be(new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(1234560));
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.BulkCopy)]
        [Trait("UpgradeSensitive", Timestamptz)]
        public void Write_DateTimeOfAnyKindIntoTimestamp_KeepsWallClockAndReadsAsUnspecified(DateTimeKind kind, TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("timestamp");

            WriteValue(table, writePath, new DateTime(2020, 1, 1, 10, 0, 0, kind));

            var value = ReadValue<DateTime>(table);
            value.Should().Be(new DateTime(2020, 1, 1, 10, 0, 0));
            value.Kind.Should().Be(DateTimeKind.Unspecified);
        }

        // Npgsql 4 threw InvalidCastException "Can't convert infinite timestamp values to DateTime"
        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_InfiniteTimestamp_ReturnsMaxValue()
        {
            var table = CreateValueTable("timestamp");
            Database.Execute($"insert into {table} values (1, 'infinity')");

            ReadValue<DateTime>(table).Should().Be(DateTime.MaxValue);
        }

        // FastSql converts the DateOnly and TimeOnly of Npgsql 10 into DateTime and TimeSpan members, which keeps models
        // written for Npgsql 4 working (ReadInfo.GetValueFromReader)
        [Fact]
        [Trait("UpgradeSensitive", DateAndTime)]
        public void Read_DateIntoDateTimeMember_ReturnsMidnightOnEveryReadPath()
        {
            var table = CreateValueTable("date");
            Database.Execute($"insert into {table} values (1, '2020-02-29'), (2, null)");
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");
            var expected = new DateTime?[] { new DateTime(2020, 2, 29), null };

            var typed = command.ExecuteQuery<ValueRow<DateTime?>>().Select(x => x.Value).ToArray();

            typed.Should().Equal(expected);
            typed[0].Value.Kind.Should().Be(DateTimeKind.Unspecified);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(DateTime?) }).Select(x => x.Value).Should().Equal(expected);
            CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<DateTime?>().Should().Equal(expected);
            command.ExecuteQuery().Select(x => x[1]).Should().Equal(new DateOnly(2020, 2, 29), DBNull.Value);
        }

        [Fact]
        [Trait("UpgradeSensitive", DateAndTime)]
        public async Task Read_TimeIntoTimeSpanMember_ReturnsTimeOfDayOnEveryReadPathAsync()
        {
            var table = CreateValueTable("time");
            Database.Execute($"insert into {table} values (1, '13:14:15.123456'), (2, null)");
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");
            var expected = new TimeSpan?[] { new TimeSpan(0, 13, 14, 15).Add(TimeSpan.FromTicks(1234560)), null };

            command.ExecuteQuery<ValueRow<TimeSpan?>>().Select(x => x.Value).Should().Equal(expected);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(TimeSpan?) }).Select(x => x.Value).Should().Equal(expected);
            CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<TimeSpan?>().Should().Equal(expected);
            (await command.ExecuteQueryAsync<ValueRow<TimeSpan?>>(cancellationToken: CancellationToken).ToListCompat(CancellationToken))
                .Select(x => x.Value).Should().Equal(expected);
            command.ExecuteQuery().Select(x => x[1]).Should().Equal(new TimeOnly(13, 14, 15, 123, 456), DBNull.Value);
        }

        // only DateTime and TimeSpan members get the conversion; members of other types fail as for any value that is not
        // IConvertible (Q52), where Npgsql 4's DateTime was converted (to string, for example)
        [Fact]
        [Trait("Quirk", "Q52")]
        [Trait("UpgradeSensitive", DateAndTime)]
        public void Read_DateIntoStringMember_ThrowsInvalidCastException()
        {
            var table = CreateValueTable("date");
            Database.Execute($"insert into {table} values (1, '2020-02-29')");

            Action act = () => ReadValue<string>(table);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object must implement IConvertible.");
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.BulkCopy)]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Timestamptz)]
        public void Write_DateTimeOfAnyKindIntoTimestamptz_StoresWallClockAsUtc(DateTimeKind kind, TypeMappingWritePath writePath)
        {
            AssertSessionTimeZoneIsUtc();
            var table = CreateValueTable("timestamptz");

            // Utc is written as timestamptz, Unspecified and Local as timestamp, which the server takes as session time (UTC);
            // so every Kind stores the wall clock as UTC, which reads back as a UTC DateTime (Npgsql 4 read a local one)
            WriteValue(table, writePath, new DateTime(2020, 1, 1, 10, 0, 0, kind));

            var value = ReadValue<DateTime>(table);
            value.Kind.Should().Be(DateTimeKind.Utc);
            value.Should().Be(new DateTime(2020, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        }

        // Npgsql 4 wrote the instant (read back as 07:00 UTC)
        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Timestamptz)]
        public void Write_DateTimeOffsetWithNonZeroOffsetIntoTimestamptz_ThrowsArgumentException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("timestamptz");

            Action act = () => WriteValue(table, writePath, new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.FromHours(3)));

            act.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Cannot write DateTimeOffset with Offset=03:00:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported.*");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Timestamptz)]
        public void Read_Timestamptz_ReturnsUtcDateTimeOnEveryReadPath()
        {
            var table = CreateValueTable("timestamptz");
            Database.Execute($"insert into {table} values (1, '2020-01-01 10:00:00+03'), (2, null)");
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");
            var instant = new DateTime(2020, 1, 1, 7, 0, 0, DateTimeKind.Utc);

            var typed = command.ExecuteQuery<ValueRow<DateTime?>>().Select(x => x.Value).ToArray();
            var anonymous = command.ExecuteQueryAnonymous(new { Id = 0, Value = default(DateTime?) }).Select(x => x.Value).ToArray();
            var firstColumn = CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<DateTime?>().ToArray();
            var untyped = command.ExecuteQuery().Select(x => x[1]).ToArray();

            foreach (var values in new[] { typed, anonymous, firstColumn })
            {
                values[0].Value.Kind.Should().Be(DateTimeKind.Utc);
                values[0].Value.Should().Be(instant);
                values[1].Should().BeNull();
            }

            untyped[0].Should().BeOfType<DateTime>().Which.Kind.Should().Be(DateTimeKind.Utc);
            untyped[0].Should().Be(instant);
            untyped[1].Should().BeSameAs(DBNull.Value);
        }

        [Theory]
        [InlineData("ExecuteQuery<T>")]
        [InlineData("ExecuteQueryFirstColumn<T>")]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Timestamptz)]
        public void Read_TimestamptzIntoDateTimeOffset_ThrowsInvalidCastException(string readPath)
        {
            var table = CreateValueTable("timestamptz");
            Database.Execute($"insert into {table} values (1, '2020-01-01 10:00:00+03')");

            Action act = readPath == "ExecuteQuery<T>"
                ? (Action)(() => CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table}").ExecuteQuery<ValueRow<DateTimeOffset>>().ToList())
                : () => ReadValue<DateTimeOffset>(table);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Invalid cast from 'System.DateTime' to 'System.DateTimeOffset'.");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_TimeSpanWithTicksIntoInterval_TruncatesToMicroseconds(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("interval");

            WriteValue(table, writePath, TimeSpan.MaxValue);

            ReadValue<TimeSpan>(table).Should().Be(TimeSpan.MaxValue.Subtract(TimeSpan.FromTicks(7)));
        }

        // Npgsql 4 counted 30 days per month
        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_IntervalWithMonths_ThrowsInvalidCastException()
        {
            var table = CreateValueTable("interval");
            Database.Execute($"insert into {table} values (1, '1 year 2 mons 3 days')");

            Action act = () => ReadValue<TimeSpan>(table);

            act.Should().ThrowExactly<InvalidCastException>()
                .WithMessage("Cannot read interval values with non-zero months as TimeSpan, since that type doesn't support months.*");
        }

        #endregion

        #region Numbers

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DecimalWithExtraScale_IsRoundedHalfAwayFromZero(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("numeric(10,2)");

            WriteValue(table, writePath, 123.455m);

            ReadValue<decimal>(table).Should().Be(123.46m);
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_Numeric_HasScaleOfColumn(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("numeric(18,4)");

            WriteValue(table, writePath, 1.5m);

            ReadValue<decimal>(table).ToString(CultureInfo.InvariantCulture).Should().Be("1.5000");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DecimalBeyondPrecision_ThrowsPostgresException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("numeric(5,2)");

            Action act = () => WriteValue(table, writePath, 12345.6m);

            act.Should().ThrowExactly<PostgresException>().WithMessage("22003: numeric field overflow*");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_NumericBeyondSystemDecimal_ThrowsOverflowException()
        {
            var table = CreateValueTable("numeric");
            Database.Execute($"insert into {table} values (1, 1e30)");

            Action act = () => ReadValue<decimal>(table);

            act.Should().ThrowExactly<OverflowException>().WithMessage("Value was either too large or too small for a Decimal.");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_NumericNaN_ThrowsInvalidCastException()
        {
            var table = CreateValueTable("numeric");
            Database.Execute($"insert into {table} values (1, 'NaN')");

            Action act = () => ReadValue<decimal>(table);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Numeric NaN not supported by System.Decimal");
        }

        [Theory]
        [InlineData("integer", (ushort)5)]
        [InlineData("bigint", (uint)5)]
        [InlineData("numeric", ulong.MaxValue)]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_UnsignedValue_ThrowsInvalidCastException<T>(string columnType, T value)
        {
            var table = CreateValueTable(columnType);
            var message = UnsupportedWriteMessage(typeof(T));

            Action insert = () => WriteValue(table, TypeMappingWritePath.Insert, value);
            Action bulkCopy = () => WriteValue(table, TypeMappingWritePath.BulkCopy, value);

            insert.Should().ThrowExactly<InvalidCastException>().WithMessage(message);
            bulkCopy.Should().ThrowExactly<InvalidCastException>().WithMessage(message);
        }

        #endregion

        #region Strings and json

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_TooLongString_ThrowsPostgresException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("varchar(2)");

            Action act = () => WriteValue(table, writePath, "abc");

            act.Should().ThrowExactly<PostgresException>().WithMessage("22001: value too long for type character varying(2)");
        }

        [Theory]
        [InlineData("json")]
        [InlineData("jsonb")]
        [Trait("Quirk", "Q86")]
        [Trait("UpgradeSensitive", TextParameter)]
        public void Insert_StringIntoJson_ThrowsPostgresException(string columnType)
        {
            var table = CreateValueTable(columnType);

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, "{}");

            act.Should().ThrowExactly<PostgresException>().WithMessage($"42804: column \"Value\" is of type {columnType} but expression is of type text*");
        }

        [Fact]
        [Trait("Quirk", "Q86")]
        [Trait("UpgradeSensitive", BinaryCopy)]
        public void BulkCopy_StringIntoJsonb_ThrowsPostgresException()
        {
            var table = CreateValueTable("jsonb");

            // binary jsonb starts with a version byte, the text's first character '{' (123) is taken for it
            Action act = () => WriteValue(table, TypeMappingWritePath.BulkCopy, "{\"a\": 1}");

            act.Should().ThrowExactly<PostgresException>().WithMessage("XX000: unsupported jsonb version number 123");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_Jsonb_ReturnsNormalizedString()
        {
            var table = CreateValueTable("jsonb");
            // the way to write jsonb through the library: a cast in the command text
            CreateContext().CreateSimple($"insert into {table} values (@p0, cast(@p1 as jsonb)), (@p2, null)", 1, "{\"b\": 1,  \"a\": [1, 2]}", 2)
                .ExecuteNonQuery();
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");
            const string normalized = "{\"a\": [1, 2], \"b\": 1}";

            command.ExecuteQuery<ValueRow<string>>().Select(x => x.Value).Should().Equal(normalized, null);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(string) }).Select(x => x.Value).Should().Equal(normalized, null);
            CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<string>().Should().Equal(normalized, null);
            command.ExecuteQuery().Select(x => x[1]).Should().Equal(normalized, DBNull.Value);
        }

        #endregion

        #region Arrays

        [Theory]
        [InlineData("integer[]", TypeMappingWritePath.Insert)]
        [InlineData("integer[]", TypeMappingWritePath.BulkCopy)]
        [InlineData("text[]", TypeMappingWritePath.Insert)]
        [InlineData("text[]", TypeMappingWritePath.BulkCopy)]
        [Trait("Quirk", "Q88")]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_ArrayMemberWithDefaultOptions_LeavesColumnNull(string columnType, TypeMappingWritePath writePath)
        {
            var table = CreateValueTable(columnType);

            // the default FromTypeOption skips collection members, so the command has no Value column at all
            if (columnType == "integer[]")
            {
                WriteValue(table, writePath, new[] { 1, 2, 3 });
            }
            else
            {
                WriteValue(table, writePath, new[] { "a", "b" });
            }

            CountRows(table).Should().Be(1);
            ReadRawValue(table).Should().BeSameAs(DBNull.Value);
        }

        [Fact]
        [Trait("Quirk", "Q88")]
        [Trait("UpgradeSensitive", Driver)]
        public void ExecuteQuery_ArrayMemberWithDefaultOptions_StaysNull()
        {
            var table = CreateValueTable("integer[]");
            Database.Execute($"insert into {table} values (1, '{{1,2}}')");

            var row = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table}").ExecuteQuery<ValueRow<int[]>>().Single();

            row.Id.Should().Be(1);
            row.Value.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_IntegerArrayWithCollectionOption_ReadsBackAsArray(TypeMappingWritePath writePath)
        {
            AssertArrayRoundTrip("integer[]", writePath, new[] { new[] { 1, 2, 3 }, new int[0], null });
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_TextArrayWithCollectionOption_ReadsBackAsArray(TypeMappingWritePath writePath)
        {
            AssertArrayRoundTrip("text[]", writePath, new[] { new[] { "a", null, "Ж" }, new string[0], null });
        }

        // writes and reads with FromTypeOption.Collection, which every path except ExecuteQueryAnonymous needs
        private void AssertArrayRoundTrip<T>(string columnType, TypeMappingWritePath writePath, T[][] values)
        {
            var table = CreateValueTable(columnType);
            var context = CreateContext();
            var rows = values.Select((x, i) => new ValueRow<T[]>() { Id = i + 1, Value = x }).ToArray();

            if (writePath == TypeMappingWritePath.Insert)
            {
                var insert = context.CommandCompilator.CompileInsert<ValueRow<T[]>>(table, FromTypeOption.All);
                foreach (var row in rows)
                {
                    context.CreateMapped(insert, row).ExecuteNonQuery();
                }
            }
            else
            {
                rows.ToDataReader(FromTypeOption.All).WriteToServer(context, table);
            }

            var command = context.CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");
            command.ExecuteQuery<ValueRow<T[]>>(new ReadOptions(fromTypeOption: FromTypeOption.All))
                .Should().BeEquivalentTo(rows, o => o.WithStrictOrdering());
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(T[]) }).Should().BeEquivalentTo(rows, o => o.WithStrictOrdering());
            context.CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<T[]>()
                .Should().BeEquivalentTo(values, o => o.WithStrictOrdering());
            var untyped = command.ExecuteQuery().Select(x => x[1]).ToArray();
            untyped.Take(values.Length - 1).Should().AllBeOfType<T[]>();
            untyped.Last().Should().BeSameAs(DBNull.Value);
        }

        #endregion

        #region Enums

        [Fact]
        [Trait("UpgradeSensitive", UnmappedEnum)]
        public void Read_EnumTypeCreatedAfterPoolLoadedTypes_ThrowsInvalidCastException()
        {
            var connectionString = CreateConnectionStringWithOwnPool();
            try
            {
                var context = Spec.DefaultProvider.CreateContext(connectionString);
                // the pool loads the database types when its first connection opens
                context.CreateSimple("select 1").ExecuteQueryFirstColumn<int>().Single();
                var table = CreateEnumTable("'Happy'");

                Action act = () => context.CreateSimple($"select \"Value\" from {table}").ExecuteQueryFirstColumn<Mood>().ToList();

                act.Should().ThrowExactly<InvalidCastException>().WithMessage("Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'");
            }
            finally
            {
                ClearPool(connectionString);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", UnmappedEnum)]
        public void Read_EnumType_ReturnsStringThatParsesIntoClrEnum()
        {
            var table = CreateEnumTable("'Happy'", "null");
            var connectionString = CreateConnectionStringWithOwnPool();
            try
            {
                var context = Spec.DefaultProvider.CreateContext(connectionString);
                var command = context.CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");

                command.ExecuteQuery().Select(x => x[1]).Should().Equal("Happy", DBNull.Value);
                command.ExecuteQuery<ValueRow<Mood?>>().Select(x => x.Value).Should().Equal(Mood.Happy, null);
                command.ExecuteQueryAnonymous(new { Id = 0, Value = default(Mood?) }).Select(x => x.Value).Should().Equal(Mood.Happy, null);
                context.CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<Mood?>().Should().Equal(Mood.Happy, null);
                context.CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<string>().Should().Equal("Happy", null);
            }
            finally
            {
                ClearPool(connectionString);
            }
        }

        [Fact]
        [Trait("Quirk", "Q86")]
        [Trait("UpgradeSensitive", TextParameter)]
        public void Insert_StringIntoEnumType_ThrowsPostgresException()
        {
            var table = CreateEnumTable();
            var enumType = ReadColumnType(table);

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, "Happy");

            act.Should().ThrowExactly<PostgresException>().WithMessage($"42804: column \"Value\" is of type {enumType} but expression is of type text*");
        }

        [Fact]
        [Trait("Quirk", "Q86")]
        [Trait("UpgradeSensitive", BinaryCopy)]
        public void BulkCopy_StringIntoEnumType_IsWritten()
        {
            var table = CreateEnumTable();

            WriteValue(table, TypeMappingWritePath.BulkCopy, "Happy");

            CreateContext().CreateSimple($"select \"Value\"::text from {table}").ExecuteQueryFirstColumn<string>().Single().Should().Be("Happy");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("Quirk", "Q89")]
        [Trait("UpgradeSensitive", UnmappedEnum)]
        public void Write_ClrEnumIntoEnumType_ThrowsInvalidCastException(TypeMappingWritePath writePath)
        {
            var table = CreateEnumTable();

            Action act = () => WriteValue(table, writePath, Mood.Happy);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage(UnsupportedWriteMessage(typeof(Mood)));
        }

        [Theory]
        [InlineData("integer", TypeMappingWritePath.Insert)]
        [InlineData("integer", TypeMappingWritePath.BulkCopy)]
        [InlineData("text", TypeMappingWritePath.Insert)]
        [InlineData("text", TypeMappingWritePath.BulkCopy)]
        [Trait("Quirk", "Q89")]
        [Trait("UpgradeSensitive", UnmappedEnum)]
        public void Write_ClrEnumIntoIntegerOrTextColumn_ThrowsInvalidCastException(string columnType, TypeMappingWritePath writePath)
        {
            var table = CreateValueTable(columnType);

            Action act = () => WriteValue(table, writePath, Mood.Happy);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage(UnsupportedWriteMessage(typeof(Mood)));
        }

        [Theory]
        [InlineData("smallint")]
        [InlineData("integer")]
        [InlineData("bigint")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_EnumFromIntegerColumn_ConvertsUnderlyingValue(string columnType)
        {
            var table = CreateValueTable(columnType);
            Database.Execute($"insert into {table} values (1, 2), (2, null)");
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");

            command.ExecuteQuery<ValueRow<Mood?>>().Select(x => x.Value).Should().Equal(Mood.Happy, null);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(Mood?) }).Select(x => x.Value).Should().Equal(Mood.Happy, null);
            CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<Mood?>().Should().Equal(Mood.Happy, null);
        }

        [Theory]
        [InlineData("varchar(10)")]
        [InlineData("text")]
        [InlineData("char(10)")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_EnumFromStringColumn_ParsesNameOrNumber(string columnType)
        {
            var table = CreateValueTable(columnType);
            Database.Execute($"insert into {table} values (1, 'Happy'), (2, '1'), (3, null)");
            var command = CreateContext().CreateSimple($"select \"Id\", \"Value\" from {table} order by \"Id\"");

            // char(10) pads 'Happy' with spaces, Enum.Parse ignores them
            command.ExecuteQuery<ValueRow<Mood?>>().Select(x => x.Value).Should().Equal(Mood.Happy, Mood.Ok, null);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(Mood?) }).Select(x => x.Value).Should().Equal(Mood.Happy, Mood.Ok, null);
            CreateContext().CreateSimple($"select \"Value\" from {table} order by \"Id\"").ExecuteQueryFirstColumn<Mood?>().Should().Equal(Mood.Happy, Mood.Ok, null);
        }

        private enum Mood
        {
            Sad = 0,
            Ok = 1,
            Happy = 2
        }

        #endregion

        #region Helpers

        // Npgsql 4 threw NotSupportedException for these values ("The CLR type ... isn't natively supported by Npgsql",
        // "The CLR enum type ... must be registered with Npgsql")
        private static string UnsupportedWriteMessage(Type type)
        {
            return $"Writing values of '{type.FullName}' is not supported for parameters having no NpgsqlDbType or DataTypeName. "
                + "Try setting one of these values to the expected database type..";
        }

        private void AssertSessionTimeZoneIsUtc()
        {
            // expected values assume the server's default session time zone in the container
            CreateContext().CreateSimple("select current_setting('TimeZone')").ExecuteQueryFirstColumn<string>().Single().Should().Be("UTC");
        }

        // a PostgreSQL enum type of its own and a table with a column of that type; returns the table name
        private string CreateEnumTable(params string[] values)
        {
            var enumType = "mood_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Database.Execute($"create type {enumType} as enum ('Sad', 'Ok', 'Happy')");
            var table = CreateValueTable(enumType);
            for (int i = 0; i < values.Length; i++)
            {
                Database.Execute($"insert into {table} values ({i + 1}, {values[i]})");
            }

            return table;
        }

        private string ReadColumnType(string table)
        {
            return CreateContext().CreateSimple($"select udt_name from information_schema.columns where table_name = '{table}' and column_name = 'Value'")
                .ExecuteQueryFirstColumn<string>().Single();
        }

        // Npgsql 4 caches the database types per connection string: a new application name gives a new pool and a fresh load
        private string CreateConnectionStringWithOwnPool()
        {
            return new NpgsqlConnectionStringBuilder(Database.ConnectionString)
            {
                ApplicationName = "fastsql-types-" + Guid.NewGuid().ToString("N")
            }.ConnectionString;
        }

        private static void ClearPool(string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(connection);
            }
        }

        #endregion
    }
}
