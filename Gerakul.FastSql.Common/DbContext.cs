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
    }
}
