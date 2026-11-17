using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// A context over a SQL Server transaction.
/// </summary>
public class SqlTransactionContext : TransactionContext, ISqlCommandCreator
{
    /// <summary>
    /// Creates a context over the transaction, with the options of the provider or with the given ones.
    /// </summary>
    public SqlTransactionContext(SqlContextProvider contextProvider, SqlTransaction transaction)
        : this(contextProvider, transaction,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the transaction, with the options of the provider or with the given ones.
    /// </summary>
    protected internal SqlTransactionContext(ContextProvider contextProvider, SqlTransaction transaction,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, transaction, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    public override DbCommand CreateCommand(string commandText)
    {
        return new SqlCommand(commandText, (SqlConnection?)Transaction.Connection, (SqlTransaction)Transaction);
    }
}
