using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// A context over an open PostgreSQL connection.
/// </summary>
public class NpgsqlConnectionContext : ConnectionContext
{
    /// <summary>
    /// Creates a context over the connection, with the options of the provider or with the given ones.
    /// </summary>
    public NpgsqlConnectionContext(NpgsqlContextProvider contextProvider, NpgsqlConnection connection)
        : this(contextProvider, connection,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the connection, with the options of the provider or with the given ones.
    /// </summary>
    protected internal NpgsqlConnectionContext(ContextProvider contextProvider, NpgsqlConnection connection,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, connection, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    public override DbCommand CreateCommand(string commandText)
    {
        return new NpgsqlCommand(commandText, (NpgsqlConnection)Connection);
    }
}
