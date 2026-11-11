using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class NpgsqlQueryOptionsTests : QueryOptionsContractTests<NpgsqlQueryOptions, NpgsqlQueryOptionsTests.Probe>
    {
        public class Probe : NpgsqlQueryOptions, IQueryOptionsProbe
        {
            public Probe(bool defaultOptions)
                : base(defaultOptions)
            {
            }

            public Probe(QueryOptions source)
                : base(source)
            {
            }

            public QueryOptions DefaultOptions => Default;

            public QueryOptions CallSetDefaults(QueryOptions defaultOptions)
            {
                return SetDefaults(defaultOptions);
            }

            public QueryOptions CallSetDefaults()
            {
                return SetDefaults();
            }
        }

        protected override NpgsqlQueryOptions Create(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
        {
            return new NpgsqlQueryOptions(commandTimeoutSeconds, caseSensitiveParamsMatching);
        }

        protected override Probe CreateProbe(bool defaultOptions)
        {
            return new Probe(defaultOptions);
        }

        protected override Probe CreateProbe(QueryOptions source)
        {
            return new Probe(source);
        }

        [Fact]
        public void Clone_OfSubclass_ReturnsNpgsqlQueryOptions()
        {
            new Probe(new QueryOptions(30, true)).Clone().Should().BeOfType<NpgsqlQueryOptions>();
        }

        [Fact]
        public void CopyConstructor_OptionsOfOtherProvider_CopiesValues()
        {
            var options = new Probe(new SqlQueryOptions(30, true));

            options.CommandTimeoutSeconds.Should().Be(30);
            options.CaseSensitiveParamsMatching.Should().BeTrue();
        }
    }
}
