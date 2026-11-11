using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Exposes the protected members of a QueryOptions type; each test class derives one from the type it tests
    public interface IQueryOptionsProbe
    {
        QueryOptions DefaultOptions { get; }
        QueryOptions CallSetDefaults(QueryOptions defaultOptions);
        QueryOptions CallSetDefaults();
    }

    // What QueryOptions, SqlQueryOptions and NpgsqlQueryOptions have in common. Concrete classes:
    // Common/QueryOptionsTests, SqlServer/SqlQueryOptionsTests, PostgreSQL/NpgsqlQueryOptionsTests.
    public abstract class QueryOptionsContractTests<TOptions, TProbe>
        where TOptions : QueryOptions
        where TProbe : TOptions, IQueryOptionsProbe
    {
        // the public constructor
        protected abstract TOptions Create(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null);

        // the protected constructor (bool defaultOptions)
        protected abstract TProbe CreateProbe(bool defaultOptions);

        // the protected copy constructor
        protected abstract TProbe CreateProbe(QueryOptions source);

        [Fact]
        public void Constructor_Arguments_AreStored()
        {
            var options = Create(30, true);

            options.Should().BeOfType<TOptions>();
            options.CommandTimeoutSeconds.Should().Be(30);
            options.CaseSensitiveParamsMatching.Should().BeTrue();
        }

        [Fact]
        public void Constructor_NoArguments_LeavesValuesUnset()
        {
            var options = Create();

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Fact]
        public void Clone_CopiesEveryPropertyIntoNewInstanceOfSameType()
        {
            var options = Create(30, false);

            var clone = options.Clone();

            clone.Should().BeOfType<TOptions>().And.NotBeSameAs(options);
            clone.CommandTimeoutSeconds.Should().Be(30);
            clone.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void Clone_ReturnsIndependentInstance()
        {
            var options = Create(30, false);

            var clone = options.Clone();
            clone.CommandTimeoutSeconds = 60;
            options.CaseSensitiveParamsMatching = true;

            options.CommandTimeoutSeconds.Should().Be(30);
            clone.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void Default_HasTimeoutUnsetAndCaseInsensitiveParameters()
        {
            var defaultOptions = CreateProbe(false).DefaultOptions;

            defaultOptions.Should().BeOfType<TOptions>();
            defaultOptions.CommandTimeoutSeconds.Should().BeNull();
            defaultOptions.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void Default_IsOneSharedInstance()
        {
            CreateProbe(false).DefaultOptions.Should().BeSameAs(CreateProbe(true).DefaultOptions);
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsTrue_SetsDefaultValues()
        {
            var options = CreateProbe(true);

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsFalse_LeavesValuesUnset()
        {
            var options = CreateProbe(false);

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Fact]
        public void CopyConstructor_BaseOptions_CopiesValues()
        {
            var options = CreateProbe(new QueryOptions(30, true));

            options.CommandTimeoutSeconds.Should().Be(30);
            options.CaseSensitiveParamsMatching.Should().BeTrue();
        }

        [Fact]
        public void CopyConstructor_UnsetValues_StayUnset()
        {
            var options = CreateProbe(new QueryOptions());

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Fact]
        public void SetDefaults_FillsOnlyUnsetValues()
        {
            var options = CreateProbe(new QueryOptions(null, true));
            var defaults = new QueryOptions(45, false);

            var result = options.CallSetDefaults(defaults);

            result.Should().BeSameAs(options);
            options.CommandTimeoutSeconds.Should().Be(45);
            options.CaseSensitiveParamsMatching.Should().BeTrue();
            defaults.CommandTimeoutSeconds.Should().Be(45);
            defaults.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void SetDefaults_AllValuesSet_KeepsThem()
        {
            var options = CreateProbe(new QueryOptions(10, false));

            options.CallSetDefaults(new QueryOptions(45, true));

            options.CommandTimeoutSeconds.Should().Be(10);
            options.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void SetDefaults_DefaultsUnset_LeavesValuesUnset()
        {
            var options = CreateProbe(new QueryOptions());

            options.CallSetDefaults(new QueryOptions());

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Fact]
        public void SetDefaultsWithoutArgument_UnsetValues_AreFilledFromDefault()
        {
            var options = CreateProbe(new QueryOptions());

            var result = options.CallSetDefaults();

            result.Should().BeSameAs(options);
            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeFalse();
        }

        [Fact]
        public void SetDefaultsWithoutArgument_SetValues_AreKept()
        {
            var options = CreateProbe(new QueryOptions(5, true));

            options.CallSetDefaults();

            options.CommandTimeoutSeconds.Should().Be(5);
            options.CaseSensitiveParamsMatching.Should().BeTrue();
        }
    }
}
