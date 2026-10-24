using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // ReadAllFirstColumn() returns the raw first column value (DBNull becomes null), ReadAllFirstColumn<T>() converts it
    // like the other typed methods (all conversions are in ValueConversionTests).
    public class ReadAllFirstColumnTests
    {
        public enum Color
        {
            Red = 1,
            Green = 2
        }

        private static ReaderTable People()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate");
        }

        private static ReaderTable SingleColumn<T>(params object[] values)
        {
            var table = new ReaderTable().Column<T>("Value");
            foreach (var value in values)
            {
                table.Row(value);
            }

            return table;
        }

        #region ReadAllFirstColumn()

        [Fact]
        public void ReadAllFirstColumn_Rows_ReturnsFirstColumnValues()
        {
            var result = People().CreateReader().ReadAllFirstColumn().Cast<object>().ToList();

            result.Should().Equal(1, 2);
        }

        [Fact]
        public async Task ReadAllFirstColumnAsync_Rows_ReturnsFirstColumnValuesAsync()
        {
            var result = await People().CreateReader().ReadAllFirstColumnAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(1, 2);
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAllFirstColumn_DBNull_BecomesNull()
        {
            var result = SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumn().Cast<object>().ToList();

            result.Should().Equal(1, null);
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllFirstColumnAsync_DBNull_BecomesNullAsync()
        {
            var result = await SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumnAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(1, null);
        }

        [Fact]
        public void ReadAllFirstColumn_EmptyReader_ReturnsNothing()
        {
            var result = SingleColumn<int>().CreateReader().ReadAllFirstColumn().Cast<object>().ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllFirstColumnAsync_EmptyReader_ReturnsNothingAsync()
        {
            var result = await SingleColumn<int>().CreateReader().ReadAllFirstColumnAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEmpty();
        }

        [Fact]
        public void ReadAllFirstColumn_SecondEnumeration_YieldsNothing()
        {
            var result = People().CreateReader().ReadAllFirstColumn();

            var first = result.Cast<object>().ToList();
            var second = result.Cast<object>().ToList();

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllFirstColumnAsync_SecondEnumeration_YieldsNothingAsync()
        {
            var result = People().CreateReader().ReadAllFirstColumnAsync();

            var first = await result.ToListCompat(TestContext.Current.CancellationToken);
            var second = await result.ToListCompat(TestContext.Current.CancellationToken);

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        #endregion

        #region ReadAllFirstColumn<T>()

        [Fact]
        public void ReadAllFirstColumnOfT_SameType_ReturnsValues()
        {
            var result = People().CreateReader().ReadAllFirstColumn<int>().ToList();

            result.Should().Equal(1, 2);
        }

        [Fact]
        public async Task ReadAllFirstColumnAsyncOfT_SameType_ReturnsValuesAsync()
        {
            var result = await People().CreateReader().ReadAllFirstColumnAsync<int>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(1, 2);
        }

        [Fact]
        public void ReadAllFirstColumnOfT_OtherType_ConvertsValues()
        {
            var result = People().CreateReader().ReadAllFirstColumn<long>().ToList();

            result.Should().Equal(1L, 2L);
        }

        [Fact]
        public async Task ReadAllFirstColumnAsyncOfT_OtherType_ConvertsValuesAsync()
        {
            var result = await People().CreateReader().ReadAllFirstColumnAsync<long>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(1L, 2L);
        }

        [Fact]
        public void ReadAllFirstColumnOfT_NullableAndReferenceTypes_ReturnNullForDBNull()
        {
            var numbers = SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumn<int?>().ToList();
            var strings = SingleColumn<string>("a", null).CreateReader().ReadAllFirstColumn<string>().ToList();

            numbers.Should().Equal(1, null);
            strings.Should().Equal("a", null);
        }

        [Fact]
        public async Task ReadAllFirstColumnAsyncOfT_NullableAndReferenceTypes_ReturnNullForDBNullAsync()
        {
            var numbers = await SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumnAsync<int?>().ToListCompat(TestContext.Current.CancellationToken);
            var strings = await SingleColumn<string>("a", null).CreateReader().ReadAllFirstColumnAsync<string>().ToListCompat(TestContext.Current.CancellationToken);

            numbers.Should().Equal(1, null);
            strings.Should().Equal("a", null);
        }

        [Fact]
        public void ReadAllFirstColumnOfT_Enum_ConvertsNumbersAndNames()
        {
            var fromNumbers = SingleColumn<int>(1, 2).CreateReader().ReadAllFirstColumn<Color>().ToList();
            var fromNames = SingleColumn<string>("Red", "Green").CreateReader().ReadAllFirstColumn<Color>().ToList();
            var nullable = SingleColumn<int>(2, null).CreateReader().ReadAllFirstColumn<Color?>().ToList();

            fromNumbers.Should().Equal(Color.Red, Color.Green);
            fromNames.Should().Equal(Color.Red, Color.Green);
            nullable.Should().Equal(Color.Green, null);
        }

        [Fact]
        public async Task ReadAllFirstColumnAsyncOfT_Enum_ConvertsNumbersAndNamesAsync()
        {
            var fromNumbers = await SingleColumn<int>(1, 2).CreateReader().ReadAllFirstColumnAsync<Color>().ToListCompat(TestContext.Current.CancellationToken);
            var fromNames = await SingleColumn<string>("Red", "Green").CreateReader().ReadAllFirstColumnAsync<Color>().ToListCompat(TestContext.Current.CancellationToken);
            var nullable = await SingleColumn<int>(2, null).CreateReader().ReadAllFirstColumnAsync<Color?>().ToListCompat(TestContext.Current.CancellationToken);

            fromNumbers.Should().Equal(Color.Red, Color.Green);
            fromNames.Should().Equal(Color.Red, Color.Green);
            nullable.Should().Equal(Color.Green, null);
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAllFirstColumnOfT_DBNullIntoNonNullable_ThrowsInvalidCastExceptionOnEnumeration()
        {
            var result = SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumn<int>();

            using (var enumerator = result.GetEnumerator())
            {
                enumerator.MoveNext().Should().BeTrue();
                enumerator.Current.Should().Be(1);

                Action moveNext = () => enumerator.MoveNext();
                moveNext.Should().ThrowExactly<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
            }
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllFirstColumnAsyncOfT_DBNullIntoNonNullable_ThrowsInvalidCastExceptionOnEnumerationAsync()
        {
            var result = SingleColumn<int>(1, null).CreateReader().ReadAllFirstColumnAsync<int>();

            using (var enumerator = result.GetEnumeratorCompat(TestContext.Current.CancellationToken))
            {
                (await enumerator.MoveNextAsync()).Should().BeTrue();
                enumerator.Current.Should().Be(1);

                Func<Task> moveNext = () => enumerator.MoveNextAsync();
                await moveNext.Should().ThrowExactlyAsync<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
            }
        }

        [Fact]
        public void ReadAllFirstColumnOfT_SecondEnumeration_YieldsNothing()
        {
            var result = People().CreateReader().ReadAllFirstColumn<int>();

            var first = result.ToList();
            var second = result.ToList();

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllFirstColumnAsyncOfT_SecondEnumeration_YieldsNothingAsync()
        {
            var result = People().CreateReader().ReadAllFirstColumnAsync<int>();

            var first = await result.ToListCompat(TestContext.Current.CancellationToken);
            var second = await result.ToListCompat(TestContext.Current.CancellationToken);

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        #endregion
    }
}
