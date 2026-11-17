using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // ReadAll() / ReadAllAsync() return each row as object[] filled by IDataRecord.GetValues; GetColumnNames().
    public class ReadAllObjectsTests
    {
        private static ReaderTable People()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age")
                .Row(1, "John", 40)
                .Row(2, "Kate", null);
        }

        #region ReadAll()

        [Fact]
        public void ReadAll_Rows_ReturnsArraysOfFieldCountLength()
        {
            var result = People().CreateReader().ReadAll().ToList();

            result.Should().HaveCount(2);
            result[0].Should().Equal(1, "John", 40);
            result[1].Should().HaveCount(3);
        }

        [Fact]
        public async Task ReadAllAsync_Rows_ReturnsArraysOfFieldCountLengthAsync()
        {
            var result = await People().CreateReader().ReadAllAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().HaveCount(2);
            result[0].Should().Equal(1, "John", 40);
            result[1].Should().HaveCount(3);
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAll_DBNull_IsKept()
        {
            var result = People().CreateReader().ReadAll().ToList();

            result[1].Should().Equal(2, "Kate", DBNull.Value);
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllAsync_DBNull_IsKeptAsync()
        {
            var result = await People().CreateReader().ReadAllAsync().ToListCompat(TestContext.Current.CancellationToken);

            result[1].Should().Equal(2, "Kate", DBNull.Value);
        }

        [Fact]
        public void ReadAll_EachRow_IsNewArray()
        {
            var result = People().CreateReader().ReadAll().ToList();

            result[0].Should().NotBeSameAs(result[1]);
        }

        [Fact]
        public async Task ReadAllAsync_EachRow_IsNewArrayAsync()
        {
            var result = await People().CreateReader().ReadAllAsync().ToListCompat(TestContext.Current.CancellationToken);

            result[0].Should().NotBeSameAs(result[1]);
        }

        [Fact]
        public void ReadAll_InterfaceOnlyReader_ReturnsArrays()
        {
            var result = People().CreateInterfaceOnlyReader().ReadAll().ToList();

            result.Should().HaveCount(2);
            result[0].Should().Equal(1, "John", 40);
            result[1].Should().Equal(2, "Kate", DBNull.Value);
        }

        [Fact]
        public async Task ReadAllAsync_InterfaceOnlyReader_ReturnsArraysAsync()
        {
            var result = await People().CreateInterfaceOnlyReader().ReadAllAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().HaveCount(2);
            result[0].Should().Equal(1, "John", 40);
            result[1].Should().Equal(2, "Kate", DBNull.Value);
        }

        [Fact]
        public void ReadAll_EmptyReader_ReturnsNothing()
        {
            var reader = new ReaderTable().Column<int>("ID").CreateReader();

            reader.ReadAll().Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllAsync_EmptyReader_ReturnsNothingAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").CreateReader();

            var result = await reader.ReadAllAsync().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEmpty();
        }

        [Fact]
        public void ReadAll_SecondEnumeration_YieldsNothing()
        {
            var result = People().CreateReader().ReadAll();

            var first = result.ToList();
            var second = result.ToList();

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllAsync_SecondEnumeration_YieldsNothingAsync()
        {
            var result = People().CreateReader().ReadAllAsync();

            var first = await result.ToListCompat(TestContext.Current.CancellationToken);
            var second = await result.ToListCompat(TestContext.Current.CancellationToken);

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        #endregion

        #region GetColumnNames()

        [Fact]
        public void GetColumnNames_Reader_ReturnsNamesInColumnOrder()
        {
            var names = People().CreateReader().GetColumnNames().ToList();

            names.Should().Equal("ID", "Name", "Age");
        }

        [Fact]
        public void GetColumnNames_DuplicateNames_ReturnsThemAsReported()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Other").CreateInterfaceOnlyReader("ID", "name", "ID");

            var names = reader.GetColumnNames().ToList();

            names.Should().Equal("ID", "name", "ID");
        }

        [Fact]
        public void GetColumnNames_ReaderWithoutColumns_ReturnsNothing()
        {
            var names = new ReaderTable().CreateReader().GetColumnNames().ToList();

            names.Should().BeEmpty();
        }

        [Fact]
        public void GetColumnNames_Reader_DoesNotAdvanceReader()
        {
            var reader = People().CreateReader();

            reader.GetColumnNames().ToList();
            var rows = reader.ReadAll().ToList();

            rows.Should().HaveCount(2);
        }

        #endregion
    }
}
