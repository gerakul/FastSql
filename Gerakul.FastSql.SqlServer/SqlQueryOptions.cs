using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlQueryOptions : QueryOptions
    {
        private static readonly SqlQueryOptions defaultOptions = new SqlQueryOptions(true);

        protected override QueryOptions Default => defaultOptions;

        public SqlQueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
            : base(commandTimeoutSeconds, caseSensitiveParamsMatching)
        {
        }

        protected SqlQueryOptions(bool defaultOptions)
            : base(defaultOptions)
        {
        }

        protected internal SqlQueryOptions(QueryOptions queryOptions)
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
            return new SqlQueryOptions(this);
        }
    }
}
