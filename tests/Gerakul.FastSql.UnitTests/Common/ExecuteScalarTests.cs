using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // I13: ExecuteScalar<T> / ExecuteScalarAsync<T> convert the result of ExecuteScalar as the values of a query are
    // converted. The command here returns what the test gives it; the drivers are covered by the integration tests.
    public class ExecuteScalarTests
    {
        public enum Color
        {
            Red,
            Green
        }

        // an IWrappedCommand of another library: only the scalar methods are implemented
        private class ScalarCommand : IWrappedCommand
        {
            private readonly object result;

            public ScalarCommand(object result)
            {
                this.result = result;
            }

            public CancellationToken LastToken { get; private set; }

            public object ExecuteScalar()
            {
                return result;
            }

            public Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                LastToken = cancellationToken;
                return Task.FromResult(result);
            }

            public int ExecuteNonQuery() => throw new NotSupportedException();
            public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public IEnumerable<object[]> ExecuteQuery() => throw new NotSupportedException();
            public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null) => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null) => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public IEnumerable<object> ExecuteQueryFirstColumn() => throw new NotSupportedException();
            public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public IEnumerable<T> ExecuteQueryFirstColumn<T>() => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
            public void UseReader(Action<DbDataReader> action) => throw new NotSupportedException();
            public Task UseReaderAsync(CancellationToken cancellationToken, Func<DbDataReader, Task> action) => throw new NotSupportedException();
        }

        private static T Scalar<T>(object result)
        {
            return new ScalarCommand(result).ExecuteScalar<T>();
        }

        [Fact]
        public void ExecuteScalar_ValueOfTheType_ReturnsIt()
        {
            Scalar<int>(5).Should().Be(5);
            Scalar<string>("abc").Should().Be("abc");
            Scalar<object>(5m).Should().Be(5m);
            Scalar<int?>(5).Should().Be(5);
        }

        // SQL Server returns the result of scope_identity() and of count_big(*) as decimal and long
        [Fact]
        public void ExecuteScalar_ValueOfAnotherType_ConvertsIt()
        {
            Scalar<int>(5m).Should().Be(5);
            Scalar<int>(5L).Should().Be(5);
            Scalar<long?>(5).Should().Be(5L);
            Scalar<decimal>(5).Should().Be(5m);
            Scalar<string>(5).Should().Be("5");
            Scalar<bool>(1).Should().BeTrue();
        }

        [Fact]
        public void ExecuteScalar_EnumAndDates_ConvertsAsTheMappingDoes()
        {
            Scalar<Color>(1).Should().Be(Color.Green);
            Scalar<Color>("Green").Should().Be(Color.Green);
            Scalar<Color?>(0).Should().Be(Color.Red);
            Scalar<DateOnly>(new DateTime(2020, 2, 29, 13, 14, 15)).Should().Be(new DateOnly(2020, 2, 29));
            Scalar<DateTime>(new DateOnly(2020, 2, 29)).Should().Be(new DateTime(2020, 2, 29));
            Scalar<TimeOnly?>(new TimeSpan(13, 14, 15)).Should().Be(new TimeOnly(13, 14, 15));
        }

        [Fact]
        public void ExecuteScalar_StringToNumber_UsesTheInvariantCulture()
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            try
            {
                Scalar<decimal>("1.5").Should().Be(1.5m);
                Scalar<string>(1.5m).Should().Be("1.5");
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        // ExecuteScalar returns null when the query has no row, and DBNull for a null value
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExecuteScalar_NoRowOrNull_ReturnsNullForATypeThatHasNull(bool dbNull)
        {
            object result = dbNull ? DBNull.Value : null;

            Scalar<int?>(result).Should().BeNull();
            Scalar<string>(result).Should().BeNull();
            Scalar<object>(result).Should().BeNull();
            Scalar<Color?>(result).Should().BeNull();
        }

        [Fact]
        public void ExecuteScalar_NoRowOrNull_ThrowsForATypeWithoutNull()
        {
            Action noRow = () => Scalar<int>(null);
            Action dbNull = () => Scalar<int>(DBNull.Value);
            Action enumeration = () => Scalar<Color>(DBNull.Value);

            noRow.Should().ThrowExactly<InvalidCastException>().WithMessage("Cannot read the result (no row) into Int32");
            dbNull.Should().ThrowExactly<InvalidCastException>().WithMessage("Cannot read the result (null) into Int32");
            enumeration.Should().ThrowExactly<InvalidCastException>().WithMessage("Cannot read the result (null) into Color");
        }

        [Fact]
        public void ExecuteScalar_ValueThatCannotBeConverted_ThrowsWithTheValueTypeAndTheTargetType()
        {
            Action text = () => Scalar<int>("abc");
            Action guid = () => Scalar<int>(Guid.Empty);
            Action tooLarge = () => Scalar<int>(long.MaxValue);

            text.Should().ThrowExactly<FormatException>().WithMessage("Cannot read the result (String) into Int32: *");
            guid.Should().ThrowExactly<InvalidCastException>().WithMessage("Cannot read the result (Guid) into Int32: *");
            tooLarge.Should().ThrowExactly<OverflowException>().WithMessage("Cannot read the result (Int64) into Int32: *");
        }

        [Fact]
        public async Task ExecuteScalarAsync_ConvertsAndPassesTheTokenAsync()
        {
            var command = new ScalarCommand(5m);
            using var source = new CancellationTokenSource();

            int value = await command.ExecuteScalarAsync<int>(source.Token);
            int? none = await new ScalarCommand(null).ExecuteScalarAsync<int?>(TestContext.Current.CancellationToken);
            Func<Task> act = () => new ScalarCommand(DBNull.Value).ExecuteScalarAsync<int>(TestContext.Current.CancellationToken);

            value.Should().Be(5);
            command.LastToken.Should().Be(source.Token);
            none.Should().BeNull();
            await act.Should().ThrowExactlyAsync<InvalidCastException>();
        }

        // the untyped method of the interface is still the one that is called without a type argument
        [Fact]
        public void ExecuteScalar_WithoutTypeArgument_IsTheInterfaceMethod()
        {
            IWrappedCommand command = new ScalarCommand(5m);

            object untyped = command.ExecuteScalar();

            untyped.Should().BeOfType<decimal>();
        }
    }
}
