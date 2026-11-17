using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// A context over an open SQL Server connection.
/// </summary>
public class SqlConnectionContext : ConnectionContext, ISqlCommandCreator
{
    /// <summary>
    /// Creates a context over the connection, with the options of the provider or with the given ones.
    /// </summary>
    public SqlConnectionContext(SqlContextProvider contextProvider, SqlConnection connection)
        : this(contextProvider, connection,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the connection, with the options of the provider or with the given ones.
    /// </summary>
    protected internal SqlConnectionContext(ContextProvider contextProvider, SqlConnection connection,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, connection, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    public override DbCommand CreateCommand(string commandText)
    {
        return new SqlCommand(commandText, (SqlConnection)Connection);
    }
}
