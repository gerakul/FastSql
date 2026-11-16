using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// A context over a PostgreSQL transaction.
/// </summary>
public class NpgsqlTransactionContext : TransactionContext
{
    /// <summary>
    /// Creates a context over the transaction, with the options of the provider or with the given ones.
    /// </summary>
    public NpgsqlTransactionContext(NpgsqlContextProvider contextProvider, NpgsqlTransaction transaction)
        : this(contextProvider, transaction,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the transaction, with the options of the provider or with the given ones.
    /// </summary>
    protected internal NpgsqlTransactionContext(ContextProvider contextProvider, NpgsqlTransaction transaction,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, transaction, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    public override DbCommand CreateCommand(string commandText)
    {
        return new NpgsqlCommand(commandText, (NpgsqlConnection?)Transaction.Connection, (NpgsqlTransaction)Transaction);
    }
}
