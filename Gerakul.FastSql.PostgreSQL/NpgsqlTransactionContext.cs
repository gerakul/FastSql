using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlTransactionContext : TransactionContext
    {
        public NpgsqlTransactionContext(NpgsqlContextProvider contextProvider, NpgsqlTransaction transaction)
            : this(contextProvider, transaction,
                  contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
        {
        }

        protected internal NpgsqlTransactionContext(ContextProvider contextProvider, NpgsqlTransaction transaction,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, transaction, queryOptions, bulkOptions, readOptions)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new NpgsqlCommand(commandText, (NpgsqlConnection)Transaction.Connection, (NpgsqlTransaction)Transaction);
        }
    }
}
