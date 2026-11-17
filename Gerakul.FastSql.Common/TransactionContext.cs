using System.Data.Common;

namespace Gerakul.FastSql.Common;

public abstract class TransactionContext : ScopedContext
{
    public DbTransaction Transaction { get; }

    protected internal TransactionContext(ContextProvider contextProvider, DbTransaction transaction,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
        // a missing transaction is not rejected here: it has always been accepted, and fails when a command is created
        this.Transaction = transaction;
    }
}
