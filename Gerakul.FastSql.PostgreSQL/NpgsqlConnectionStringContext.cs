using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// A context over a PostgreSQL connection string.
/// </summary>
public class NpgsqlConnectionStringContext : ConnectionStringContext
{
    /// <summary>
    /// Creates a context over the connection string, with the options of the provider or with the given ones.
    /// </summary>
    public NpgsqlConnectionStringContext(NpgsqlContextProvider contextProvider, string connectionString)
        : this(contextProvider, connectionString,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the connection string, with the options of the provider or with the given ones.
    /// </summary>
    protected internal NpgsqlConnectionStringContext(ContextProvider contextProvider, string connectionString,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, connectionString, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    protected override DbConnection CreateConnection()
    {
        return new NpgsqlConnection(ConnectionString);
    }
}
