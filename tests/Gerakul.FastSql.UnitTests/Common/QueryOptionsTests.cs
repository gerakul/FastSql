using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    public class QueryOptionsTests : QueryOptionsContractTests<QueryOptions, QueryOptionsTests.Probe>
    {
        public class Probe : QueryOptions, IQueryOptionsProbe
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

        protected override QueryOptions Create(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
        {
            return new QueryOptions(commandTimeoutSeconds, caseSensitiveParamsMatching);
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
        public void Clone_OfSubclass_ReturnsBaseQueryOptions()
        {
            var clone = new Probe(new QueryOptions(30, true)).Clone();

            clone.Should().BeOfType<QueryOptions>();
            clone.CommandTimeoutSeconds.Should().Be(30);
            clone.CaseSensitiveParamsMatching.Should().BeTrue();
        }
    }
}
