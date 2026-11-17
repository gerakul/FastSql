using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Exposes the protected members of a BulkOptions type; each test class derives one from the type it tests
    public interface IBulkOptionsProbe
    {
        BulkOptions DefaultOptions { get; }
        BulkOptions CallSetDefaults(BulkOptions defaultOptions);
    }

    // The base BulkOptions values, which BulkOptions, SqlBulkOptions and NpgsqlBulkOptions share. Concrete classes:
    // Common/BulkOptionsTests, SqlServer/SqlBulkOptionsTests, PostgreSQL/NpgsqlBulkOptionsTests.
    public abstract class BulkOptionsContractTests<TOptions, TProbe>
        where TOptions : BulkOptions
        where TProbe : TOptions, IBulkOptionsProbe
    {
        // the public constructor without arguments
        protected abstract TOptions Create();

        // the protected constructor (bool defaultOptions)
        protected abstract TProbe CreateProbe(bool defaultOptions);

        // the protected copy constructor
        protected abstract TProbe CreateProbe(BulkOptions source);

        protected static BulkOptions BaseValues()
        {
            return new BulkOptions(FieldsSelector.Common, true, true, true, true);
        }

        [Fact]
        public void Constructor_NoArguments_LeavesBaseValuesUnset()
        {
            var options = Create();

            options.Should().BeOfType<TOptions>();
            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
        }

        [Fact]
        public void Clone_CopiesBaseValuesIntoNewInstanceOfSameType()
        {
            var options = Create();
            options.FieldsSelector = FieldsSelector.Both;
            options.CaseSensitiveFieldsMatching = false;
            options.CreateTable = true;
            options.IgnoreDataReaderSchemaTable = false;
            options.CheckTableIfNotExistsBeforeCreation = true;

            var clone = options.Clone();

            clone.Should().BeOfType<TOptions>().And.NotBeSameAs(options);
            clone.FieldsSelector.Should().Be(FieldsSelector.Both);
            clone.CaseSensitiveFieldsMatching.Should().BeFalse();
            clone.CreateTable.Should().BeTrue();
            clone.IgnoreDataReaderSchemaTable.Should().BeFalse();
            clone.CheckTableIfNotExistsBeforeCreation.Should().BeTrue();
        }

        [Fact]
        public void Clone_ReturnsIndependentInstance()
        {
            var options = Create();
            options.FieldsSelector = FieldsSelector.Source;

            var clone = options.Clone();
            clone.FieldsSelector = FieldsSelector.Destination;
            options.CreateTable = true;

            options.FieldsSelector.Should().Be(FieldsSelector.Source);
            clone.CreateTable.Should().BeNull();
        }

        [Fact]
        public void Default_HasBaseDefaultValues()
        {
            var defaultOptions = CreateProbe(false).DefaultOptions;

            defaultOptions.Should().BeOfType<TOptions>();
            defaultOptions.FieldsSelector.Should().Be(FieldsSelector.Source);
            defaultOptions.CaseSensitiveFieldsMatching.Should().BeNull();
            defaultOptions.CreateTable.Should().BeFalse();
            defaultOptions.IgnoreDataReaderSchemaTable.Should().BeFalse();
            defaultOptions.CheckTableIfNotExistsBeforeCreation.Should().BeFalse();
        }

        [Fact]
        public void Default_IsOneSharedInstance()
        {
            CreateProbe(false).DefaultOptions.Should().BeSameAs(CreateProbe(true).DefaultOptions);
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsTrue_SetsBaseDefaultValues()
        {
            var options = CreateProbe(true);

            options.FieldsSelector.Should().Be(FieldsSelector.Source);
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeFalse();
            options.IgnoreDataReaderSchemaTable.Should().BeFalse();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeFalse();
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsFalse_LeavesBaseValuesUnset()
        {
            var options = CreateProbe(false);

            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
        }

        [Fact]
        public void CopyConstructor_BaseOptions_CopiesBaseValues()
        {
            var options = CreateProbe(BaseValues());

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeTrue();
            options.IgnoreDataReaderSchemaTable.Should().BeTrue();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeTrue();
        }

        [Fact]
        public void SetDefaults_FillsOnlyUnsetBaseValues()
        {
            var options = CreateProbe(new BulkOptions(fieldsSelector: FieldsSelector.Destination, createTable: false));
            var defaults = BaseValues();

            var result = options.CallSetDefaults(defaults);

            result.Should().BeSameAs(options);
            options.FieldsSelector.Should().Be(FieldsSelector.Destination);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.CreateTable.Should().BeFalse();
            options.IgnoreDataReaderSchemaTable.Should().BeTrue();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeTrue();
            defaults.FieldsSelector.Should().Be(FieldsSelector.Common);
            defaults.CreateTable.Should().BeTrue();
        }

        [Fact]
        public void SetDefaults_DefaultsUnset_LeavesBaseValuesUnset()
        {
            var options = CreateProbe(new BulkOptions());

            options.CallSetDefaults(new BulkOptions());

            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
        }

        [Fact]
        public void SetDefaults_FromOwnDefault_LeavesCaseSensitivityUnset()
        {
            var options = CreateProbe(new BulkOptions());

            options.CallSetDefaults(options.DefaultOptions);

            options.FieldsSelector.Should().Be(FieldsSelector.Source);
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeFalse();
            options.IgnoreDataReaderSchemaTable.Should().BeFalse();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeFalse();
        }
    }
}
