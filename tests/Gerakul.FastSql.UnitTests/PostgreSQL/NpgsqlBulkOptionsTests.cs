using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;
using NpgsqlColumnDefinitionOptions = Gerakul.FastSql.PostgreSQL.ColumnDefinitionOptions;

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

        private static NpgsqlColumnDefinitionOptions NewColumnDefinitionOptions()
        {
            return new NpgsqlColumnDefinitionOptions()
            {
                PrimaryKey = new List<string>() { "ID" },
                StringMaxLength = 100,
                Precision = 10,
                Scale = 2,
                DateTimeScale = 3,
                ThrowIfUnsupportedType = true
            };
        }

        [Fact]
        public void Constructor_AllArguments_AreStored()
        {
            var columnDefinitionOptions = NewColumnDefinitionOptions();

            var options = new NpgsqlBulkOptions(FieldsSelector.Common, true, true, columnDefinitionOptions, false, true, 60);

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeTrue();
            options.ColumnDefinitionOptions.Should().BeSameAs(columnDefinitionOptions);
            options.IgnoreDataReaderSchemaTable.Should().BeFalse();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeTrue();
            options.BulkCopyTimeout.Should().Be(60);
        }

        // the two arguments of the constructor before phase 5 keep their places
        [Fact]
        public void Constructor_FirstTwoArguments_AreStoredAndOtherValuesStayUnset()
        {
            var options = new NpgsqlBulkOptions(FieldsSelector.Common, true);

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeNull();
            options.ColumnDefinitionOptions.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
            options.BulkCopyTimeout.Should().BeNull();
        }

        [Fact]
        public void NpgsqlBulkOptions_DeclaresColumnDefinitionOptionsOnly()
        {
            typeof(NpgsqlBulkOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(x => x.Name).Should().Equal("ColumnDefinitionOptions");
        }

        [Fact]
        public void Default_HasStaticColumnDefinitionOptions()
        {
            ((NpgsqlBulkOptions)CreateProbe(false).DefaultOptions).ColumnDefinitionOptions.Should().BeSameAs(NpgsqlColumnDefinitionOptions.Default);
        }

        [Fact]
        public void ProtectedConstructor_SetsOrLeavesColumnDefinitionOptions()
        {
            new Probe(true).ColumnDefinitionOptions.Should().BeSameAs(NpgsqlColumnDefinitionOptions.Default);
            new Probe(false).ColumnDefinitionOptions.Should().BeNull();
        }

        [Fact]
        public void CopyConstructor_NpgsqlBulkOptions_SharesColumnDefinitionOptions()
        {
            var source = new NpgsqlBulkOptions(columnDefinitionOptions: NewColumnDefinitionOptions());

            new Probe(source).ColumnDefinitionOptions.Should().BeSameAs(source.ColumnDefinitionOptions);
            ((NpgsqlBulkOptions)source.Clone()).ColumnDefinitionOptions.Should().BeSameAs(source.ColumnDefinitionOptions);
        }

        [Fact]
        public void CopyConstructor_BaseOptions_LeavesColumnDefinitionOptionsUnset()
        {
            new Probe(BaseValues()).ColumnDefinitionOptions.Should().BeNull();
        }

        [Fact]
        public void SetDefaults_NpgsqlBulkOptions_ClonesColumnDefinitionOptionsWhenUnset()
        {
            var defaults = new NpgsqlBulkOptions(columnDefinitionOptions: NewColumnDefinitionOptions());
            var options = new Probe(new NpgsqlBulkOptions());

            options.CallSetDefaults(defaults);

            options.ColumnDefinitionOptions.Should().NotBeSameAs(defaults.ColumnDefinitionOptions);
            options.ColumnDefinitionOptions.Should().BeEquivalentTo(defaults.ColumnDefinitionOptions);
            options.ColumnDefinitionOptions.PrimaryKey.Should().NotBeSameAs(defaults.ColumnDefinitionOptions.PrimaryKey);
        }

        [Fact]
        public void SetDefaults_NpgsqlBulkOptions_KeepsOwnColumnDefinitionOptions()
        {
            var own = NewColumnDefinitionOptions();
            var options = new Probe(new NpgsqlBulkOptions(columnDefinitionOptions: own));

            options.CallSetDefaults(new NpgsqlBulkOptions(columnDefinitionOptions: NewColumnDefinitionOptions()));

            options.ColumnDefinitionOptions.Should().BeSameAs(own);
        }

        [Fact]
        public void SetDefaults_BaseOptions_LeavesColumnDefinitionOptionsUnset()
        {
            var options = new Probe(new NpgsqlBulkOptions());

            options.CallSetDefaults(BaseValues());

            options.CreateTable.Should().BeTrue();
            options.ColumnDefinitionOptions.Should().BeNull();
        }
    }
}
