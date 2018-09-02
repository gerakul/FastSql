namespace Gerakul.FastSql.Common
{
    public class QueryOptions
    {
        private static readonly QueryOptions defaultOptions = new QueryOptions(true);

        public int? CommandTimeoutSeconds { get; set; }
        public bool? CaseSensitiveParamsMatching { get; set; }

        protected virtual QueryOptions Default => defaultOptions;

        public QueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
        {
            this.CommandTimeoutSeconds = commandTimeoutSeconds;
            this.CaseSensitiveParamsMatching = caseSensitiveParamsMatching;
        }

        protected QueryOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                this.CommandTimeoutSeconds = null;
                this.CaseSensitiveParamsMatching = false;
            }
        }

        protected QueryOptions(QueryOptions queryOptions)
        {
            this.CommandTimeoutSeconds = queryOptions.CommandTimeoutSeconds;
            this.CaseSensitiveParamsMatching = queryOptions.CaseSensitiveParamsMatching;
        }

        protected internal virtual QueryOptions SetDefaults(QueryOptions defaultOptions)
        {
            if (!CommandTimeoutSeconds.HasValue)
            {
                CommandTimeoutSeconds = defaultOptions.CommandTimeoutSeconds;
            }

            if (!CaseSensitiveParamsMatching.HasValue)
            {
                CaseSensitiveParamsMatching = defaultOptions.CaseSensitiveParamsMatching;
            }

            return this;
        }

        protected internal virtual QueryOptions SetDefaults()
        {
            return SetDefaults(Default);
        }

        public virtual QueryOptions Clone()
        {
            return new QueryOptions(this);
        }
    }
}
