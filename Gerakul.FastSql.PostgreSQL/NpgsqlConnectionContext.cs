using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlConnectionContext : ConnectionContext
    {
        public NpgsqlConnectionContext(ContextProvider contextProvider, NpgsqlConnection connection) 
            : base(contextProvider, connection)
        {
        }

        public override DbCommand CreateCommand(string commandText)
        {
            return new NpgsqlCommand(commandText, (NpgsqlConnection)Connection);
        }
    }
}
