using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlConnectionContext : ConnectionContext
    {
        public NpgsqlConnectionContext(NpgsqlContextProvider contextProvider, NpgsqlConnection connection)
            : this(contextProvider, connection,
                  contextProvider.DefaultQueryOptions, contextProvider.DefaultBulkOptions, contextProvider.DefaultReadOptions)
        {
        }

        protected internal NpgsqlConnectionContext(ContextProvider contextProvider, NpgsqlConnection connection,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, connection, queryOptions, bulkOptions, readOptions)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new NpgsqlCommand(commandText, (NpgsqlConnection)Connection);
        }
    }
}
