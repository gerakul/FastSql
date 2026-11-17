using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A context over a transaction. Its commands run on the connection of the transaction, inside it; the context
/// neither commits nor rolls back.
/// </summary>
public abstract class TransactionContext : ScopedContext
{
    /// <summary>
    /// The transaction of the context.
    /// </summary>
    public DbTransaction Transaction { get; }

    /// <summary>
    /// Creates a context over the transaction with copies of the given options.
    /// </summary>
    protected internal TransactionContext(ContextProvider contextProvider, DbTransaction transaction,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
        // a missing transaction is not rejected here: it has always been accepted, and fails when a command is created
        this.Transaction = transaction;
    }
}
