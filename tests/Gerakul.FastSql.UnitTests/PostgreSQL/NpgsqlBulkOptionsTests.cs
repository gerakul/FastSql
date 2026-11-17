using System.Reflection;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class NpgsqlBulkOptionsTests : BulkOptionsContractTests<NpgsqlBulkOptions, NpgsqlBulkOptionsTests.Probe>
    {
        public class Probe : NpgsqlBulkOptions, IBulkOptionsProbe
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

        protected override NpgsqlBulkOptions Create()
        {
            return new NpgsqlBulkOptions();
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
        public void Constructor_Arguments_AreStoredAndOtherBaseValuesStayUnset()
        {
            var options = new NpgsqlBulkOptions(FieldsSelector.Common, true);

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
        }

        [Fact]
        [Trait("Quirk", "Q10")]
        public void NpgsqlBulkOptions_DeclaresNoPropertiesOfItsOwn()
        {
            typeof(NpgsqlBulkOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Should().BeEmpty();
        }
    }
}
