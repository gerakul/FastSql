using System.Data.Common;

namespace Gerakul.FastSql.Common;

internal sealed class DataReaderAttachedToConnectionStringContext : IBulkWriter
{
    private readonly DbDataReader reader;
    private readonly ConnectionStringContext connectionStringContext;

    public DbContext Context => connectionStringContext;

    internal DataReaderAttachedToConnectionStringContext(DbDataReader reader, ConnectionStringContext context)
    {
        this.reader = reader;
        this.connectionStringContext = context;
    }

    #region IBulkWriter

    public void WriteToServer(string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        connectionStringContext.UsingConnection(x =>
        {
            reader.GetBulkWriter(x).WriteToServer(destinationTable, bulkOptions, fields);
        });
    }

    public async Task WriteToServerAsync(string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        await connectionStringContext.UsingConnectionAsync(async x =>
        {
            await reader.GetBulkWriter(x).WriteToServerAsync(destinationTable, bulkOptions, cancellationToken, fields).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
