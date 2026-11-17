using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// A context over a SQL Server connection string.
/// </summary>
public class SqlConnectionStringContext : ConnectionStringContext, ISqlCommandCreator
{
    /// <summary>
    /// Creates a context over the connection string, with the options of the provider or with the given ones.
    /// </summary>
    public SqlConnectionStringContext(SqlContextProvider contextProvider, string connectionString)
        : this(contextProvider, connectionString,
              contextProvider.QueryOptions, contextProvider.BulkOptions, contextProvider.ReadOptions)
    {
    }

    /// <summary>
    /// Creates a context over the connection string, with the options of the provider or with the given ones.
    /// </summary>
    protected internal SqlConnectionStringContext(ContextProvider contextProvider, string connectionString,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, connectionString, queryOptions, bulkOptions, readOptions)
    {
    }

    /// <inheritdoc />
    protected override DbConnection CreateConnection()
    {
        return new SqlConnection(ConnectionString);
    }
}
