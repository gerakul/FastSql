using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlTransactionContext : TransactionContext
    {
        public NpgsqlTransactionContext(ContextProvider contextProvider, NpgsqlTransaction transaction)
        : base(contextProvider, transaction)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new NpgsqlCommand(commandText, (NpgsqlConnection)Transaction.Connection, (NpgsqlTransaction)Transaction);
        }
    }
}
