using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlConnectionStringContext : ConnectionStringContext
    {
        public NpgsqlConnectionStringContext(NpgsqlContextProvider contextProvider, string connectionString)
            : this(contextProvider, connectionString, 
                  contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
        {
        }

        protected internal NpgsqlConnectionStringContext(ContextProvider contextProvider, string connectionString,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions) 
            : base(contextProvider, connectionString, queryOptions, bulkOptions, readOptions)
        {
        }

        protected override DbConnection CreateConnection()
        {
            return new NpgsqlConnection(ConnectionString);
        }
    }
}
