using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlTransactionContext : TransactionContext
    {
        public SqlTransactionContext(ContextProvider contextProvider, SqlTransaction transaction)
        : base(contextProvider, transaction)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new SqlCommand(commandText, (SqlConnection)Transaction.Connection, (SqlTransaction)Transaction);
        }
    }
}
