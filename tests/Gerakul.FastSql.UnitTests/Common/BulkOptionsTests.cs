using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    public class BulkOptionsTests : BulkOptionsContractTests<BulkOptions, BulkOptionsTests.Probe>
    {
        public class Probe : BulkOptions, IBulkOptionsProbe
        {
            public Probe(bool defaultOptions)
                : base(defaultOptions)
            {
            }

            public Probe(BulkOptions source)
                : base(source)
            {
            }

            public BulkOptions DefaultOptions => Default;

            public BulkOptions CallSetDefaults(BulkOptions defaultOptions)
            {
                return SetDefaults(defaultOptions);
            }
        }

        protected override BulkOptions Create()
        {
            return new BulkOptions();
        }

        protected override Probe CreateProbe(bool defaultOptions)
        {
            return new Probe(defaultOptions);
        }

        protected override Probe CreateProbe(BulkOptions source)
        {
            return new Probe(source);
        }

        [Fact]
        public void Constructor_Arguments_AreStored()
        {
            var options = new BulkOptions(FieldsSelector.Destination, true, false, true, false);

            options.FieldsSelector.Should().Be(FieldsSelector.Destination);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeFalse();
            options.IgnoreDataReaderSchemaTable.Should().BeTrue();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeFalse();
        }

        [Fact]
        public void Clone_OfSubclass_ReturnsBaseBulkOptions()
        {
            var clone = new Probe(BaseValues()).Clone();

            clone.Should().BeOfType<BulkOptions>();
            clone.FieldsSelector.Should().Be(FieldsSelector.Common);
        }
    }
}
