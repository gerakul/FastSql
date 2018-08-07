using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlConnectionContext : ConnectionContext
    {
        public SqlConnectionContext(ContextProvider contextProvider, SqlConnection connection) 
            : base(contextProvider, connection)
        {
        }

        protected override DbCommand CreateCommand(string commandText)
        {
            return new SqlCommand(commandText, (SqlConnection)Connection);
        }
    }
}
