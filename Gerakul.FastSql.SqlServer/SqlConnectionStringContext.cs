using Gerakul.FastSql.Common;
using System.Data.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlConnectionStringContext : ConnectionStringContext, ISqlCommandCreator
    {
        public SqlConnectionStringContext(SqlContextProvider contextProvider, string connectionString)
            : this(contextProvider, connectionString,
                  contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
        {
        }

        protected internal SqlConnectionStringContext(ContextProvider contextProvider, string connectionString,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, connectionString, queryOptions, bulkOptions, readOptions)
        {
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
