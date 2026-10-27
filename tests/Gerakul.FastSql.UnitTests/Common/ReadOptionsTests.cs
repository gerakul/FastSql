using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // ReadOptions has no protected members, so its defaults and SetDefaults are observed through reading,
    // which fills the passed instance itself. Precedence at execution against a database is covered by the
    // integration tests.
    public class ReadOptionsTests
    {
        private class Row
        {
            public int ID { get; set; }
        }

        private static DataTable Table()
        {
            var table = new DataTable();
            table.Columns.Add("ID", typeof(int));
            table.Rows.Add(1);
            return table;
        }

        [Fact]
        public void Constructor_Arguments_AreStored()
        {
            var options = new ReadOptions(FieldsSelector.Common, true, FromTypeOption.PublicField);

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.FromTypeOption.Should().Be(FromTypeOption.PublicField);
        }

        [Fact]
        public void Constructor_NoArguments_LeavesValuesUnset()
        {
            var options = new ReadOptions();

            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
        }

        [Fact]
        public void Clone_CopiesEveryPropertyIntoNewReadOptions()
        {
            var options = new ReadOptions(FieldsSelector.Both, false, FromTypeOption.All);

            var clone = options.Clone();

            clone.Should().BeOfType<ReadOptions>().And.NotBeSameAs(options);
            clone.FieldsSelector.Should().Be(FieldsSelector.Both);
            clone.CaseSensitiveFieldsMatching.Should().BeFalse();
            clone.FromTypeOption.Should().Be(FromTypeOption.All);
        }

        [Fact]
        public void Clone_ReturnsIndependentInstance()
        {
            var options = new ReadOptions(FieldsSelector.Both);

            var clone = options.Clone();
            clone.FieldsSelector = FieldsSelector.Source;
            options.CaseSensitiveFieldsMatching = true;

            options.FieldsSelector.Should().Be(FieldsSelector.Both);
            clone.CaseSensitiveFieldsMatching.Should().BeNull();
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public void ReadAll_UnsetOptions_StayUnset()
        {
            var options = new ReadOptions();

            var rows = Table().CreateDataReader().ReadAll<Row>(options);

            rows.ToList().Should().ContainSingle();
            options.FieldsSelector.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public void ReadAll_SetOptions_AreKept()
        {
            var options = new ReadOptions(FieldsSelector.Common, true, FromTypeOption.PublicProperty);

            Table().CreateDataReader().ReadAll<Row>(options).ToList();

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.FromTypeOption.Should().Be(FromTypeOption.PublicProperty);
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public void ReadAll_PartlySetOptions_AreNotModified()
        {
            var options = new ReadOptions(caseSensitiveFieldsMatching: true);

            Table().CreateDataReader().ReadAll<Row>(options).ToList();

            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.FromTypeOption.Should().BeNull();
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public async Task ReadAllAsync_UnsetOptions_StayUnsetAsync()
        {
            var options = new ReadOptions();

            await Table().CreateDataReader().ReadAllAsync<Row>(options).ToListCompat(TestContext.Current.CancellationToken);

            options.FieldsSelector.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public void ReadAllAnonymous_UnsetOptions_StayUnset()
        {
            var options = new ReadOptions();

            Table().CreateDataReader().ReadAllAnonymous(new { ID = 0 }, options).ToList();

            options.FieldsSelector.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
        }
    }
}
