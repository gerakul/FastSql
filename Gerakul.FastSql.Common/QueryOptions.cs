namespace Gerakul.FastSql.Common
{
    public class QueryOptions
    {
        private static readonly QueryOptions defaultOptions = new QueryOptions(true);

        public int? CommandTimeoutSeconds { get; set; }

        protected virtual QueryOptions Default => defaultOptions;

        public QueryOptions(int? commandTimeoutSeconds = null)
        {
            this.CommandTimeoutSeconds = commandTimeoutSeconds;
        }

        protected QueryOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                this.CommandTimeoutSeconds = null;
            }
        }

        protected QueryOptions(QueryOptions queryOptions)
        {
            this.CommandTimeoutSeconds = queryOptions.CommandTimeoutSeconds;
        }

        protected internal virtual QueryOptions SetDefaults(QueryOptions defaultOptions)
        {
            if (!CommandTimeoutSeconds.HasValue)
            {
                CommandTimeoutSeconds = defaultOptions.CommandTimeoutSeconds;
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
