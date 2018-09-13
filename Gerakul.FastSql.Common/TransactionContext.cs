using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class TransactionContext : ScopedContext
    {
        public DbTransaction Transaction { get; }

        protected internal TransactionContext(ContextProvider contextProvider, DbTransaction transaction,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, queryOptions, bulkOptions, readOptions)
        {
            this.Transaction = transaction;
        }
    }
}
