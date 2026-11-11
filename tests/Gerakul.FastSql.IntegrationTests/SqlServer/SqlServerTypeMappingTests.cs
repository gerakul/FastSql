using System;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    // I6 for SQL Server: values written through Insert (parameters) and bulk copy, read back through every read path.
    // Everything here depends on the driver, Microsoft.Data.SqlClient (UpgradeSensitive).
    [Trait("Provider", "SqlServer")]
    public class SqlServerTypeMappingTests : TypeMappingTests<SqlServerDatabase>
    {
        private const string Driver = "Microsoft.Data.SqlClient: CLR types returned and values written";
        private const string DateTimeParameter = "SqlClient sends DateTime parameters as datetime";
        private const string BulkCopyScale = "SqlBulkCopy rounds extra decimal scale (since Microsoft.Data.SqlClient 2.0) but truncates extra datetimeoffset scale";

        private static readonly Guid sampleGuid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        // values that both write paths store unchanged (or change the same way); the differences are in the edge tests below
        private static readonly TypeCase[] cases = new TypeCase[]
        {
            new TypeCase<bool, bool?>("bit", new[] { false, true }),
            new TypeCase<byte, byte?>("tinyint", new[] { byte.MinValue, byte.MaxValue }),
            new TypeCase<short, short?>("smallint", new[] { short.MinValue, (short)0, short.MaxValue }),
            new TypeCase<int, int?>("int", new[] { int.MinValue, 0, int.MaxValue }),
            new TypeCase<long, long?>("bigint", new[] { long.MinValue, 0L, long.MaxValue }),
            // the widest values System.Decimal holds at scale 4
            new TypeCase<decimal, decimal?>("decimal(28,4)", new[] { -999999999999999999999999.9999m, 0m, 1.5m, 999999999999999999999999.9999m }),
            new TypeCase<decimal, decimal?>("numeric(10,3)", new[] { -9999999.999m, 0.001m, 9999999.999m }),
            new TypeCase<decimal, decimal?>("money", new[] { -922337203685477.5808m, 0m, 922337203685477.5807m }),
            new TypeCase<double, double?>("float", new[] { double.MinValue, 0.1, double.MaxValue }),
            new TypeCase<float, float?>("real", new[] { float.MinValue, 0.1f, float.MaxValue }),
            // from 1753: Insert sends DateTime as datetime (Q81)
            new TypeCase<DateTime, DateTime?>("date", new[] { new DateTime(1753, 1, 1), new DateTime(2020, 2, 29), new DateTime(9999, 12, 31) }),
            new TypeCase<TimeSpan, TimeSpan?>("time", new[] { TimeSpan.Zero, new TimeSpan(0, 13, 14, 15).Add(TimeSpan.FromTicks(1234567)), TimeSpan.FromDays(1).Subtract(TimeSpan.FromTicks(1)) }),
            // datetime keeps 1/300 of a second: the server rounds .1234567 to .123 and the maximum to .997
            new TypeCase<DateTime, DateTime?>("datetime",
                new[] { new DateTime(1753, 1, 1), new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(1234567), DateTime.MaxValue },
                expected: new[] { new DateTime(1753, 1, 1), new DateTime(2020, 2, 29, 13, 14, 15, 123), new DateTime(9999, 12, 31, 23, 59, 59, 997) }),
            // whole seconds from 1753: Insert sends DateTime as datetime (Q81)
            new TypeCase<DateTime, DateTime?>("datetime2", new[] { new DateTime(1753, 1, 1), new DateTime(2020, 2, 29, 13, 14, 15), new DateTime(9999, 12, 31, 23, 59, 59) }),
            // smalldatetime rounds to the minute
            new TypeCase<DateTime, DateTime?>("smalldatetime",
                new[] { new DateTime(1900, 1, 1), new DateTime(2020, 2, 29, 13, 14, 29, 998), new DateTime(2020, 2, 29, 13, 14, 30), new DateTime(2079, 6, 6, 23, 59, 0) },
                expected: new[] { new DateTime(1900, 1, 1), new DateTime(2020, 2, 29, 13, 14, 0), new DateTime(2020, 2, 29, 13, 15, 0), new DateTime(2079, 6, 6, 23, 59, 0) }),
            // the comparison is by instant; offsets are checked in Write_DateTimeOffset_KeepsOffset
            new TypeCase<DateTimeOffset, DateTimeOffset?>("datetimeoffset",
                new[] { DateTimeOffset.MinValue, new DateTimeOffset(2020, 2, 29, 13, 14, 15, TimeSpan.FromHours(5.5)).AddTicks(1234567), DateTimeOffset.MaxValue }),
            new TypeCase<string, string>("char(5)", new[] { "ab", "", "Grüße" }, expected: new[] { "ab   ", "     ", "Grüße" }),
            new TypeCase<string, string>("varchar(20)", new[] { "", "abc", "Grüße" }),
            // nchar counts UTF-16 code units: the emoji takes two
            new TypeCase<string, string>("nchar(5)", new[] { "ab", "", "Ж€😀" }, expected: new[] { "ab   ", "     ", "Ж€😀 " }),
            new TypeCase<string, string>("nvarchar(20)", new[] { "", "abc", "Привет 😀" }),
            new TypeCase<string, string>("nvarchar(max)", new[] { "", new string('x', 10000) + "Ж" }),
            new TypeCase<byte[], byte[]>("varbinary(10)", new[] { new byte[0], new byte[] { 1, 2, 3 }, Enumerable.Repeat((byte)0xFF, 10).ToArray() }),
            new TypeCase<byte[], byte[]>("varbinary(max)", new[] { new byte[0], Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray() }),
            // System.Data.SqlClient 4.5.1 could not read uniqueidentifier values on .NET 9 and later (Q80, fixed by Microsoft.Data.SqlClient)
            new TypeCase<Guid, Guid?>("uniqueidentifier", new[] { Guid.Empty, sampleGuid }),
            // xml is written and read as a string; the server normalizes it
            new TypeCase<string, string>("xml", new[] { "<a x='1'><b/></a>", "" }, expected: new[] { "<a x=\"1\"><b /></a>", "" })
        };

        public SqlServerTypeMappingTests(SqlServerDatabase database)
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

        #region Date and time

        [Theory]
        [InlineData(TypeMappingWritePath.Insert, 1233333)]
        [InlineData(TypeMappingWritePath.BulkCopy, 1234567)]
        [Trait("Quirk", "Q81")]
        [Trait("UpgradeSensitive", DateTimeParameter)]
        public void Write_DateTimeIntoDatetime2_KeepsFullPrecisionOnlyThroughBulkCopy(TypeMappingWritePath writePath, int expectedFractionTicks)
        {
            var table = CreateValueTable("datetime2");

            // Insert: the parameter is datetime, so .1234567 becomes .123 (1/300 s, really .12333...) before it reaches datetime2
            WriteValue(table, writePath, new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(1234567));

            ReadValue<DateTime>(table).Should().Be(new DateTime(2020, 2, 29, 13, 14, 15).AddTicks(expectedFractionTicks));
        }

        [Theory]
        [InlineData("date")]
        [InlineData("datetime2")]
        [Trait("Quirk", "Q81")]
        [Trait("UpgradeSensitive", DateTimeParameter)]
        public void Insert_DateTimeBefore1753IntoDateOrDatetime2_ThrowsSqlTypeException(string columnType)
        {
            var table = CreateValueTable(columnType);

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, DateTime.MinValue);

            act.Should().ThrowExactly<SqlTypeException>()
                .WithMessage("SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.");
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [InlineData("date")]
        [InlineData("datetime2")]
        [Trait("Quirk", "Q81")]
        [Trait("UpgradeSensitive", DateTimeParameter)]
        public void BulkCopy_DateTimeMinValueIntoDateOrDatetime2_IsWritten(string columnType)
        {
            var table = CreateValueTable(columnType);

            WriteValue(table, TypeMappingWritePath.BulkCopy, DateTime.MinValue);

            ReadValue<DateTime>(table).Should().Be(DateTime.MinValue);
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DateTimeMinValueIntoDatetime_ThrowsSqlTypeException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("datetime");

            // raised by SqlClient while converting the value, before anything is sent
            Action act = () => WriteValue(table, writePath, DateTime.MinValue);

            act.Should().ThrowExactly<SqlTypeException>()
                .WithMessage("SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void CreateInsert_DateTimeMinValueIntoDatetime_ThrowsOnlyWhenExecuted()
        {
            var table = CreateValueTable("datetime");

            var command = CreateContext().CreateInsert(table, new ValueRow<DateTime>() { Id = 1, Value = DateTime.MinValue });
            Action act = () => command.ExecuteNonQuery();

            act.Should().ThrowExactly<SqlTypeException>()
                .WithMessage("SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.");
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Unspecified, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Utc, TypeMappingWritePath.BulkCopy)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.Insert)]
        [InlineData(DateTimeKind.Local, TypeMappingWritePath.BulkCopy)]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DateTimeOfAnyKindIntoDatetime2_KeepsWallClockAndReadsAsUnspecified(DateTimeKind kind, TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("datetime2");

            WriteValue(table, writePath, new DateTime(2020, 1, 1, 10, 0, 0, kind));

            var value = ReadValue<DateTime>(table);
            value.Should().Be(new DateTime(2020, 1, 1, 10, 0, 0));
            value.Kind.Should().Be(DateTimeKind.Unspecified);
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_TimeSpanOfOneDayIntoTime_ThrowsOverflowException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("time");

            Action act = () => WriteValue(table, writePath, TimeSpan.FromDays(1));

            act.Should().ThrowExactly<OverflowException>()
                .WithMessage("SqlDbType.Time overflow.  Value '1.00:00:00' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_DateTimeOffset_KeepsOffset(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("datetimeoffset");

            WriteValue(table, writePath, new DateTimeOffset(2020, 2, 29, 13, 14, 15, TimeSpan.FromHours(5.5)).AddTicks(1234567));

            ReadValue<DateTimeOffset>(table).ToString("o", CultureInfo.InvariantCulture).Should().Be("2020-02-29T13:14:15.1234567+05:30");
        }

        [Theory]
        [InlineData(TypeMappingWritePath.Insert, 16)]
        [InlineData(TypeMappingWritePath.BulkCopy, 15)]
        [Trait("Quirk", "Q82")]
        [Trait("UpgradeSensitive", BulkCopyScale)]
        public void Write_DateTimeOffsetWithFractionIntoDatetimeoffset0_InsertRoundsBulkCopyTruncates(TypeMappingWritePath writePath, int expectedSecond)
        {
            var table = CreateValueTable("datetimeoffset(0)");

            WriteValue(table, writePath, new DateTimeOffset(2020, 2, 29, 13, 14, 15, 600, TimeSpan.FromHours(-8)));

            ReadValue<DateTimeOffset>(table).Should().Be(new DateTimeOffset(2020, 2, 29, 13, 14, expectedSecond, TimeSpan.FromHours(-8)));
        }

        [Fact]
        [Trait("Quirk", "Q84")]
        [Trait("UpgradeSensitive", DateTimeParameter)]
        public void Insert_DateTimeIntoDatetimeoffset_StoresZeroOffset()
        {
            var table = CreateValueTable("datetimeoffset");

            WriteValue(table, TypeMappingWritePath.Insert, new DateTime(2020, 1, 1, 10, 0, 0));

            ReadValue<DateTimeOffset>(table).ToString("o", CultureInfo.InvariantCulture).Should().Be("2020-01-01T10:00:00.0000000+00:00");
        }

        [Fact]
        [Trait("Quirk", "Q84")]
        [Trait("UpgradeSensitive", Driver)]
        public void BulkCopy_DateTimeIntoDatetimeoffset_StoresLocalOffsetOfClientMachine()
        {
            var table = CreateValueTable("datetimeoffset");
            var value = new DateTime(2020, 1, 1, 10, 0, 0);

            WriteValue(table, TypeMappingWritePath.BulkCopy, value);

            // SqlBulkCopy converts like new DateTimeOffset(value): the offset of the client's time zone (zero in CI)
            var read = ReadValue<DateTimeOffset>(table);
            read.DateTime.Should().Be(value);
            read.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(value));
        }

        [Fact]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_DatetimeoffsetIntoDateTime_ThrowsInvalidCastException()
        {
            var table = CreateValueTable("datetimeoffset");
            Database.Execute($"insert into {table} values (1, '2020-01-01 10:00 +03:00')");

            Action act = () => ReadValue<DateTime>(table);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object must implement IConvertible.");
        }

        [Fact]
        [Trait("Quirk", "Q87")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_Datetime2IntoDateTimeOffset_ThrowsInvalidCastException()
        {
            var table = CreateValueTable("datetime2");
            Database.Execute($"insert into {table} values (1, '2020-01-01 10:00')");

            Action act = () => CreateContext().CreateSimple($"select [Id], [Value] from {table}").ExecuteQuery<ValueRow<DateTimeOffset>>().ToList();

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Invalid cast from 'System.DateTime' to 'System.DateTimeOffset'.");
        }

        #endregion

        #region Numbers

        [Theory]
        [InlineData(TypeMappingWritePath.Insert, "123.46")]
        [InlineData(TypeMappingWritePath.BulkCopy, "123.46")]
        [Trait("UpgradeSensitive", BulkCopyScale)]
        public void Write_DecimalWithExtraScale_IsRoundedByBothPaths(TypeMappingWritePath writePath, string expected)
        {
            var table = CreateValueTable("decimal(10,2)");

            WriteValue(table, writePath, 123.456m);

            ReadValue<decimal>(table).Should().Be(decimal.Parse(expected, CultureInfo.InvariantCulture));
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_Decimal_HasScaleOfColumn(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("decimal(18,4)");

            WriteValue(table, writePath, 1.5m);

            ReadValue<decimal>(table).ToString(CultureInfo.InvariantCulture).Should().Be("1.5000");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Insert_DecimalBeyondPrecision_ThrowsSqlException()
        {
            var table = CreateValueTable("decimal(5,2)");

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, 12345.6m);

            act.Should().ThrowExactly<SqlException>()
                .WithMessage("Arithmetic overflow error converting numeric to data type numeric.*The statement has been terminated.");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void BulkCopy_DecimalBeyondPrecision_ThrowsInvalidOperationException()
        {
            var table = CreateValueTable("decimal(5,2)");

            Action act = () => WriteValue(table, TypeMappingWritePath.BulkCopy, 12345.6m);

            act.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("The given value '12345.6' of type Decimal from the data source cannot be converted to type decimal for Column 1 [Value].");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", "SqlDecimal to System.Decimal conversion in the driver")]
        public void Read_DecimalThatOverflowsSystemDecimalAtColumnScale_ReturnsValue(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("decimal(38,10)");

            // fits System.Decimal, but not with 10 decimal places (30 digits): the driver lowers the scale to fit
            // (System.Data.SqlClient threw OverflowException "Conversion overflows.")
            WriteValue(table, writePath, 12345678901234567890.12m);

            ReadValue<decimal>(table).ToString(CultureInfo.InvariantCulture).Should().Be("12345678901234567890.12000000");
        }

        [Theory]
        [InlineData("smallint", (sbyte)5)]
        [InlineData("int", (ushort)5)]
        [InlineData("bigint", (uint)5)]
        [InlineData("decimal(20,0)", ulong.MaxValue)]
        [Trait("UpgradeSensitive", Driver)]
        public void Insert_SignedByteOrUnsignedValue_ThrowsArgumentException<T>(string columnType, T value)
        {
            var table = CreateValueTable(columnType);

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, value);

            act.Should().ThrowExactly<ArgumentException>().WithMessage($"The parameter data type of {typeof(T).Name} is invalid.");
        }

        [Theory]
        [InlineData("smallint", (sbyte)5)]
        [InlineData("int", (ushort)5)]
        [InlineData("bigint", (uint)5)]
        [InlineData("decimal(20,0)", ulong.MaxValue)]
        [Trait("UpgradeSensitive", Driver)]
        public void BulkCopy_SignedByteOrUnsignedValue_IsConverted<T>(string columnType, T value)
        {
            var table = CreateValueTable(columnType);

            WriteValue(table, TypeMappingWritePath.BulkCopy, value);

            Convert.ToDecimal(ReadRawValue(table)).Should().Be(Convert.ToDecimal(value));
        }

        #endregion

        #region Strings and binary

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_NonLatinTextIntoVarchar_ReplacesCharactersWithQuestionMarks(TypeMappingWritePath writePath)
        {
            // the database uses the server's default collation, SQL_Latin1_General_CP1_CI_AS
            var table = CreateValueTable("varchar(20)");

            WriteValue(table, writePath, "Привет ü");

            ReadValue<string>(table).Should().Be("?????? ü");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Insert_TooLongString_ThrowsSqlException()
        {
            var table = CreateValueTable("varchar(2)");

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, "abc");

            act.Should().ThrowExactly<SqlException>()
                .WithMessage($"String or binary data would be truncated in table '{Database.DatabaseName}.dbo.{table}', column 'Value'. Truncated value: 'ab'.*");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void BulkCopy_TooLongString_ThrowsSqlException()
        {
            var table = CreateValueTable("varchar(2)");

            Action act = () => WriteValue(table, TypeMappingWritePath.BulkCopy, "abc");

            act.Should().ThrowExactly<SqlException>().WithMessage("Received an invalid column length from the bcp client for colid 2.");
        }

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_InvalidXml_ThrowsSqlException(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("xml");

            Action act = () => WriteValue(table, writePath, "not xml <");

            act.Should().ThrowExactly<SqlException>().WithMessage("XML parsing: line 1, character 9, unexpected end of input");
        }

        #endregion

        #region rowversion

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_RowWithRowversion_GetsVersionFromServerReadAsByteArray(TypeMappingWritePath writePath)
        {
            var table = Database.CreateScratchTable("[Id] int not null, [Value] rowversion");
            var rows = new[] { new ValueRow<byte[]>() { Id = 1, Value = new byte[8] }, new ValueRow<byte[]>() { Id = 2, Value = new byte[8] } };
            var context = CreateContext();

            if (writePath == TypeMappingWritePath.Insert)
            {
                // the member has to be ignored, see Insert_RowversionMember_ThrowsSqlException
                foreach (var row in rows)
                {
                    context.Insert(table, row, "Value");
                }
            }
            else
            {
                // SqlBulkCopy skips the rowversion column without an error
                rows.WriteToServer(context, table);
            }

            var command = context.CreateSimple($"select [Id], [Value] from {table} order by [Id]");
            var typed = command.ExecuteQuery<ValueRow<byte[]>>().ToArray();
            var anonymous = command.ExecuteQueryAnonymous(new { Id = 0, Value = default(byte[]) }).ToArray();
            var untyped = command.ExecuteQuery().ToArray();
            var firstColumn = context.CreateSimple($"select [Value] from {table} order by [Id]").ExecuteQueryFirstColumn<byte[]>().ToArray();

            typed.Select(x => x.Value.Length).Should().Equal(8, 8);
            BitConverter.ToUInt64(typed[1].Value.Reverse().ToArray(), 0).Should().BeGreaterThan(BitConverter.ToUInt64(typed[0].Value.Reverse().ToArray(), 0));
            anonymous.Should().BeEquivalentTo(typed, o => o.WithStrictOrdering());
            firstColumn.Should().BeEquivalentTo(typed.Select(x => x.Value), o => o.WithStrictOrdering());
            untyped.Select(x => x[1]).Should().AllBeOfType<byte[]>();
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Insert_RowversionMember_ThrowsSqlException()
        {
            var table = Database.CreateScratchTable("[Id] int not null, [Value] rowversion");

            Action act = () => WriteValue(table, TypeMappingWritePath.Insert, new byte[8]);

            act.Should().ThrowExactly<SqlException>()
                .WithMessage("Cannot insert an explicit value into a timestamp column. Use INSERT with a column list to exclude the timestamp column, "
                    + "or insert a DEFAULT into the timestamp column.");
        }

        [Fact]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_RowversionIntoUInt64_ThrowsInvalidCastException()
        {
            var table = Database.CreateScratchTable("[Id] int not null, [Value] rowversion");
            Database.Execute($"insert into {table} ([Id]) values (1)");

            Action act = () => ReadValue<ulong>(table);

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object must implement IConvertible.");
        }

        #endregion

        #region Enums

        [Theory]
        [MemberData(nameof(WritePaths))]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_EnumIntoIntColumn_StoresUnderlyingNumber(TypeMappingWritePath writePath)
        {
            var table = CreateValueTable("int");

            WriteValue(table, writePath, Color.Green);

            ReadRawValue(table).Should().Be(2);
            ReadValue<Color>(table).Should().Be(Color.Green);
        }

        [Theory]
        [InlineData(TypeMappingWritePath.Insert, "2")]
        [InlineData(TypeMappingWritePath.BulkCopy, "Green")]
        [Trait("Quirk", "Q83")]
        [Trait("UpgradeSensitive", Driver)]
        public void Write_EnumIntoStringColumn_StoresNumberThroughInsertAndNameThroughBulkCopy(TypeMappingWritePath writePath, string expected)
        {
            var table = CreateValueTable("nvarchar(20)");

            WriteValue(table, writePath, Color.Green);

            ReadRawValue(table).Should().Be(expected);
            ReadValue<Color>(table).Should().Be(Color.Green);
        }

        [Theory]
        [InlineData("tinyint")]
        [InlineData("smallint")]
        [InlineData("int")]
        [InlineData("bigint")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_EnumFromIntegerColumn_ConvertsUnderlyingValue(string columnType)
        {
            var table = CreateValueTable(columnType);
            Database.Execute($"insert into {table} values (1, 2), (2, null)");
            var command = CreateContext().CreateSimple($"select [Id], [Value] from {table} order by [Id]");

            command.ExecuteQuery<ValueRow<Color?>>().Select(x => x.Value).Should().Equal(Color.Green, null);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(Color?) }).Select(x => x.Value).Should().Equal(Color.Green, null);
            CreateContext().CreateSimple($"select [Value] from {table} order by [Id]").ExecuteQueryFirstColumn<Color?>().Should().Equal(Color.Green, null);
        }

        [Theory]
        [InlineData("varchar(10)")]
        [InlineData("nvarchar(10)")]
        [InlineData("char(10)")]
        [Trait("UpgradeSensitive", Driver)]
        public void Read_EnumFromStringColumn_ParsesNameOrNumber(string columnType)
        {
            var table = CreateValueTable(columnType);
            Database.Execute($"insert into {table} values (1, 'Green'), (2, '1'), (3, null)");
            var command = CreateContext().CreateSimple($"select [Id], [Value] from {table} order by [Id]");

            // char(10) pads 'Green' with spaces, Enum.Parse ignores them
            command.ExecuteQuery<ValueRow<Color?>>().Select(x => x.Value).Should().Equal(Color.Green, Color.Red, null);
            command.ExecuteQueryAnonymous(new { Id = 0, Value = default(Color?) }).Select(x => x.Value).Should().Equal(Color.Green, Color.Red, null);
            CreateContext().CreateSimple($"select [Value] from {table} order by [Id]").ExecuteQueryFirstColumn<Color?>().Should().Equal(Color.Green, Color.Red, null);
        }

        private enum Color
        {
            Red = 1,
            Green = 2
        }

        #endregion
    }
}
