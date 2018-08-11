using Gerakul.FastSql.Common;
using System.Data.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlConnectionContext : ConnectionContext
    {
        public SqlConnectionContext(ContextProvider contextProvider, SqlConnection connection) 
            : base(contextProvider, connection)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new SqlCommand(commandText, (SqlConnection)Connection);
        }
    }
}
