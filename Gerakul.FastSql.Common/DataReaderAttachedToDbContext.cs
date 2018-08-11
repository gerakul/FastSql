using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class DataReaderAttachedToConnectionStringContext : IBulkWriter
    {
        private DbDataReader reader;
        private ConnectionStringContext connectionStringContext;

        public DbContext Context => connectionStringContext;

        internal DataReaderAttachedToConnectionStringContext(DbDataReader reader, ConnectionStringContext context)
        {
            this.reader = reader;
            this.connectionStringContext = context;
        }

        #region IBulkWriter

        public void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            connectionStringContext.UsingConnection(x =>
            {
                reader.GetBulkWriter(x).WriteToServer(bulkOptions, destinationTable, fields);
            });
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            await connectionStringContext.UsingConnectionAsync(async x =>
            {
                await reader.GetBulkWriter(x).WriteToServerAsync(cancellationToken, bulkOptions, destinationTable, fields).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

