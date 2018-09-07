using Gerakul.FastSql.Common;
using System.Data.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlTransactionContext : TransactionContext
    {
        public SqlTransactionContext(SqlContextProvider contextProvider, SqlTransaction transaction)
            : this(contextProvider, transaction,
                  contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
        {
        }

        protected internal SqlTransactionContext(ContextProvider contextProvider, SqlTransaction transaction,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, transaction, queryOptions, bulkOptions, readOptions)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new SqlCommand(commandText, (SqlConnection)Transaction.Connection, (SqlTransaction)Transaction);
        }
    }
}
