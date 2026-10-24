using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlConnectionContext : ConnectionContext, ISqlCommandCreator
    {
        public SqlConnectionContext(SqlContextProvider contextProvider, SqlConnection connection)
            : this(contextProvider, connection,
                  contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
        {
        }

        protected internal SqlConnectionContext(ContextProvider contextProvider, SqlConnection connection,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, connection, queryOptions, bulkOptions, readOptions)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new SqlCommand(commandText, (SqlConnection)Connection);
        }
    }
}
