using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlQueryOptions : QueryOptions
    {
        private static readonly NpgsqlQueryOptions defaultOptions = new NpgsqlQueryOptions(true);

        protected override QueryOptions Default => defaultOptions;

        public NpgsqlQueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
            : base(commandTimeoutSeconds, caseSensitiveParamsMatching)
        {
        }

        protected NpgsqlQueryOptions(bool defaultOptions)
            : base(defaultOptions)
        {
        }

        protected internal NpgsqlQueryOptions(QueryOptions queryOptions)
            : base(queryOptions)
        {
        }

        protected override QueryOptions SetDefaults(QueryOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            return this;
        }

        public override QueryOptions Clone()
        {
            return new NpgsqlQueryOptions(this);
        }

    }
}
