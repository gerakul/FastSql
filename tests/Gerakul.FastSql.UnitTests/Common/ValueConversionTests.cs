using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Regression set for commit 41af0f9 ("fix for values casting issue"): a reader value is converted to the member type
    // with Enum.Parse / Enum.ToObject for enums and Convert.ChangeType otherwise (current culture, invariant in tests).
    // Every case runs through each typed reading method, because each of them builds its conversion info separately.
    public class ValueConversionTests
    {
        public class ValueHolder<T>
        {
            public T Value { get; set; }
        }

        public enum Color
        {
            Red = 1,
            Green = 2,
            Blue = 3
        }

        public static TheoryData<MappingReadMethod> Methods => new TheoryData<MappingReadMethod>(Enum.GetValues<MappingReadMethod>());

        // reads a single row with one column named Value into T
        private static Task<T> Read<T>(MappingReadMethod method, Type columnType, object value)
        {
            return Read<T>(method, new ReaderTable().Column("Value", columnType).Row(value));
        }

        private static async Task<T> Read<T>(MappingReadMethod method, ReaderTable table)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using (var reader = table.CreateReader())
            {
                switch (method)
                {
                    case MappingReadMethod.ReadAll:
                        return reader.ReadAll<ValueHolder<T>>().Single().Value;
                    case MappingReadMethod.ReadAllAsync:
                        return (await reader.ReadAllAsync<ValueHolder<T>>().ToListCompat(cancellationToken)).Single().Value;
                    case MappingReadMethod.ReadAllAnonymous:
                        return reader.ReadAllAnonymous(new { Value = default(T) }).Single().Value;
                    case MappingReadMethod.ReadAllAnonymousAsync:
                        return (await reader.ReadAllAnonymousAsync(new { Value = default(T) }).ToListCompat(cancellationToken)).Single().Value;
                    case MappingReadMethod.ReadAllFirstColumn:
                        return reader.ReadAllFirstColumn<T>().Single();
                    case MappingReadMethod.ReadAllFirstColumnAsync:
                        return (await reader.ReadAllFirstColumnAsync<T>().ToListCompat(cancellationToken)).Single();
                    default:
                        throw new ArgumentOutOfRangeException(nameof(method));
                }
            }
        }

        #region Numbers

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoLong_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<long>(method, typeof(int), 42);

            value.Should().Be(42L);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_LongIntoInt_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<int>(method, typeof(long), 42L);

            value.Should().Be(42);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_LongOutOfIntRangeIntoInt_ThrowsOverflowException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<int>(method, typeof(long), (long)int.MaxValue + 1);

            await act.Should().ThrowExactlyAsync<OverflowException>()
                .WithMessage("Value was either too large or too small for an Int32.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoDecimal_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<decimal>(method, typeof(int), 42);

            value.Should().Be(42m);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DecimalIntoDouble_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<double>(method, typeof(decimal), 12.5m);

            value.Should().Be(12.5);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_FractionalIntoInt_RoundsToEven(MappingReadMethod method)
        {
            var fromDecimal = await Read<int>(method, typeof(decimal), 2.5m);
            var fromDouble = await Read<int>(method, typeof(double), 3.5);

            fromDecimal.Should().Be(2);
            fromDouble.Should().Be(4);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_ShortIntoNullableInt_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<int?>(method, typeof(short), (short)5);

            value.Should().Be(5);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_NumericStringIntoInt_ParsesValue(MappingReadMethod method)
        {
            var value = await Read<int>(method, typeof(string), "42");

            value.Should().Be(42);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_NonNumericStringIntoInt_ThrowsFormatException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<int>(method, typeof(string), "abc");

            await act.Should().ThrowExactlyAsync<FormatException>()
                .WithMessage("The input string 'abc' was not in a correct format.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoString_FormatsValue(MappingReadMethod method)
        {
            var value = await Read<string>(method, typeof(int), 42);

            value.Should().Be("42");
        }

        #endregion

        #region Bool, Guid, byte[]

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_BoolIntoBool_KeepsValue(MappingReadMethod method)
        {
            var value = await Read<bool>(method, typeof(bool), true);

            value.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoBool_ConvertsNonZeroToTrue(MappingReadMethod method)
        {
            var one = await Read<bool>(method, typeof(int), 1);
            var zero = await Read<bool>(method, typeof(int), 0);

            one.Should().BeTrue();
            zero.Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_GuidIntoGuid_KeepsValue(MappingReadMethod method)
        {
            var guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

            var value = await Read<Guid>(method, typeof(Guid), guid);

            value.Should().Be(guid);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q52")]
        public async Task Read_GuidIntoString_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<string>(method, typeof(Guid), new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Object must implement IConvertible.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_StringIntoGuid_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<Guid>(method, typeof(string), "0f8fad5b-d9cb-469f-a165-70867728950e");

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Invalid cast from 'System.String' to 'System.Guid'.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_ByteArrayIntoByteArray_KeepsValue(MappingReadMethod method)
        {
            var value = await Read<byte[]>(method, typeof(byte[]), new byte[] { 1, 2, 3 });

            value.Should().Equal(1, 2, 3);
        }

        #endregion

        #region Date and time

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateTimeIntoDateTime_KeepsValueAndKind(MappingReadMethod method)
        {
            var dateTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);

            var value = await Read<DateTime>(method, typeof(DateTime), dateTime);

            value.Should().Be(dateTime);
            value.Kind.Should().Be(DateTimeKind.Unspecified);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_UtcDateTimeIntoDateTime_KeepsInstantAndKind(MappingReadMethod method)
        {
            var dateTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var table = new ReaderTable()
                .Column(new DataColumn("Value", typeof(DateTime)) { DateTimeMode = DataSetDateTime.Utc })
                .Row(dateTime);

            var value = await Read<DateTime>(method, table);

            value.ToUniversalTime().Should().Be(dateTime);
            value.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_StringIntoDateTime_ParsesValue(MappingReadMethod method)
        {
            var value = await Read<DateTime>(method, typeof(string), "2020-01-02 03:04:05");

            value.Should().Be(new DateTime(2020, 1, 2, 3, 4, 5));
            value.Kind.Should().Be(DateTimeKind.Unspecified);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateTimeIntoString_FormatsWithCurrentCulture(MappingReadMethod method)
        {
            var value = await Read<string>(method, typeof(DateTime), new DateTime(2020, 1, 2, 3, 4, 5));

            value.Should().Be("01/02/2020 03:04:05");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateTimeIntoDateTimeOffset_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<DateTimeOffset>(method, typeof(DateTime), new DateTime(2020, 1, 2, 3, 4, 5));

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Invalid cast from 'System.DateTime' to 'System.DateTimeOffset'.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateTimeOffsetIntoDateTimeOffset_KeepsInstantAndOffset(MappingReadMethod method)
        {
            var dateTimeOffset = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));

            var value = await Read<DateTimeOffset>(method, typeof(DateTimeOffset), dateTimeOffset);

            value.Should().Be(dateTimeOffset);
            value.Offset.Should().Be(TimeSpan.FromHours(2));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q52")]
        public async Task Read_DateTimeOffsetIntoDateTime_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<DateTime>(method, typeof(DateTimeOffset), new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero));

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Object must implement IConvertible.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_TimeSpanIntoTimeSpan_KeepsValue(MappingReadMethod method)
        {
            var value = await Read<TimeSpan>(method, typeof(TimeSpan), new TimeSpan(1, 2, 3));

            value.Should().Be(new TimeSpan(1, 2, 3));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_StringIntoTimeSpan_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<TimeSpan>(method, typeof(string), "01:02:03");

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Invalid cast from 'System.String' to 'System.TimeSpan'.");
        }

        // DateOnly and TimeOnly (what Npgsql 10 returns for date and time) are converted for DateTime and TimeSpan members
        // only, which is what Npgsql 4 returned; other members follow Q52

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateOnlyIntoDateTime_ReturnsMidnightUnspecified(MappingReadMethod method)
        {
            var value = await Read<DateTime>(method, typeof(DateOnly), new DateOnly(2020, 2, 29));

            value.Should().Be(new DateTime(2020, 2, 29));
            value.Kind.Should().Be(DateTimeKind.Unspecified);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateOnlyIntoNullableDateTime_ReturnsMidnight(MappingReadMethod method)
        {
            var value = await Read<DateTime?>(method, typeof(DateOnly), new DateOnly(2020, 2, 29));

            value.Should().Be(new DateTime(2020, 2, 29));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_TimeOnlyIntoTimeSpan_ReturnsTimeOfDay(MappingReadMethod method)
        {
            var value = await Read<TimeSpan>(method, typeof(TimeOnly), new TimeOnly(13, 14, 15, 123));

            value.Should().Be(new TimeSpan(0, 13, 14, 15, 123));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DateOnlyIntoDateOnly_KeepsValue(MappingReadMethod method)
        {
            var value = await Read<DateOnly>(method, typeof(DateOnly), new DateOnly(2020, 2, 29));

            value.Should().Be(new DateOnly(2020, 2, 29));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q52")]
        public async Task Read_DateOnlyIntoString_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<string>(method, typeof(DateOnly), new DateOnly(2020, 2, 29));

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Object must implement IConvertible.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q52")]
        public async Task Read_TimeOnlyIntoDateTime_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<DateTime>(method, typeof(TimeOnly), new TimeOnly(13, 14, 15));

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Object must implement IConvertible.");
        }

        #endregion

        #region Object members

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoObject_KeepsValue(MappingReadMethod method)
        {
            var value = await Read<object>(method, typeof(int), 42);

            value.Should().Be(42);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Fixed", "Q52")]
        public async Task Read_GuidIntoObject_ReturnsGuid(MappingReadMethod method)
        {
            var value = await Read<object>(method, typeof(Guid), new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));

            value.Should().Be(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DBNullIntoObject_ReturnsNull(MappingReadMethod method)
        {
            var value = await Read<object>(method, typeof(int), null);

            value.Should().BeNull();
        }

        #endregion

        #region Enums

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoEnum_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<Color>(method, typeof(int), 2);

            value.Should().Be(Color.Green);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_LongIntoIntBasedEnum_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<Color>(method, typeof(long), 3L);

            value.Should().Be(Color.Blue);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_UndefinedIntIntoEnum_ReturnsUndefinedValue(MappingReadMethod method)
        {
            var value = await Read<Color>(method, typeof(int), 99);

            ((int)value).Should().Be(99);
            Enum.IsDefined(value).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_NameIntoEnum_ParsesValue(MappingReadMethod method)
        {
            var value = await Read<Color>(method, typeof(string), "Green");

            value.Should().Be(Color.Green);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_NumericStringIntoEnum_ParsesValue(MappingReadMethod method)
        {
            var value = await Read<Color>(method, typeof(string), "2");

            value.Should().Be(Color.Green);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_NameInOtherCaseIntoEnum_ThrowsArgumentException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<Color>(method, typeof(string), "green");

            await act.Should().ThrowExactlyAsync<ArgumentException>()
                .WithMessage("Requested value 'green' was not found.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_UnknownNameIntoEnum_ThrowsArgumentException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<Color>(method, typeof(string), "Purple");

            await act.Should().ThrowExactlyAsync<ArgumentException>()
                .WithMessage("Requested value 'Purple' was not found.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_IntIntoNullableEnum_ConvertsValue(MappingReadMethod method)
        {
            var value = await Read<Color?>(method, typeof(int), 2);

            value.Should().Be(Color.Green);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DBNullIntoNullableEnum_ReturnsNull(MappingReadMethod method)
        {
            var value = await Read<Color?>(method, typeof(int), null);

            value.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q50")]
        public async Task Read_DBNullIntoEnum_ThrowsArgumentException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<Color>(method, typeof(int), null);

            await act.Should().ThrowExactlyAsync<ArgumentException>()
                .WithMessage("The value passed in must be an enum base or an underlying type for an enum, such as an Int32. (Parameter 'value')");
        }

        #endregion

        #region Nulls

        [Theory]
        [MemberData(nameof(Methods))]
        [Trait("Quirk", "Q12")]
        public async Task Read_DBNullIntoInt_ThrowsInvalidCastException(MappingReadMethod method)
        {
            Func<Task> act = () => Read<int>(method, typeof(int), null);

            await act.Should().ThrowExactlyAsync<InvalidCastException>()
                .WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DBNullIntoNullableInt_ReturnsNull(MappingReadMethod method)
        {
            var value = await Read<int?>(method, typeof(int), null);

            value.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DBNullIntoString_ReturnsNull(MappingReadMethod method)
        {
            var value = await Read<string>(method, typeof(string), null);

            value.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_DBNullIntoByteArray_ReturnsNull(MappingReadMethod method)
        {
            var value = await Read<byte[]>(method, typeof(byte[]), null);

            value.Should().BeNull();
        }

        #endregion
    }
}
