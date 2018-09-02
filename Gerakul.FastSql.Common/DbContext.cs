namespace Gerakul.FastSql.Common
{
    public abstract class DbContext
    {
        public ContextProvider ContextProvider { get; }

        public QueryOptions DefaultQueryOptions { get; }
        public BulkOptions DefaultBulkOptions { get; }
        public ReadOptions DefaultReadOptions { get; }

        protected internal DbContext(ContextProvider contextProvider,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            this.ContextProvider = contextProvider;
            this.DefaultQueryOptions = queryOptions.Clone();
            this.DefaultBulkOptions = bulkOptions.Clone();
            this.DefaultReadOptions = readOptions.Clone();
        }

        internal QueryOptions PrepareQueryOptions(QueryOptions options)
        {
            if (options == null)
            {
                return DefaultQueryOptions.Clone().SetDefaults();
            }

            var opt = ContextProvider.GetTyped(options, out var needClone);
            if (needClone)
            {
                opt = opt.Clone();
            }

            return opt.SetDefaults(DefaultQueryOptions).SetDefaults();
        }

        internal BulkOptions PrepareBulkOptions(BulkOptions options)
        {
            if (options == null)
            {
                return DefaultBulkOptions.Clone().SetDefaults();
            }

            var opt = ContextProvider.GetTyped(options, out var needClone);
            if (needClone)
            {
                opt = opt.Clone();
            }

            return opt.SetDefaults(DefaultBulkOptions).SetDefaults();
        }

        internal ReadOptions PrepareReadOptions(ReadOptions options)
        {
            if (options == null)
            {
                return DefaultReadOptions.Clone();
            }

            var opt = options.Clone();
            opt.SetDefaults(DefaultReadOptions);
            return opt;
        }
    }
}
