using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class DataReaderAttachedToConnectionStringContext : BulkWriter
    {
        private DbDataReader reader;
        private ConnectionStringContext ConnectionStringContext => (ConnectionStringContext)context;

        internal DataReaderAttachedToConnectionStringContext(DbDataReader reader, ConnectionStringContext context)
            : base(context)
        {
            this.reader = reader;
        }

        #region BulkWriter

        public override void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            ConnectionStringContext.UsingConnection(x =>
            {
                reader.GetBulkWriter(x).WriteToServer(bulkOptions, destinationTable, fields);
            });
        }

        public override async Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            await ConnectionStringContext.UsingConnectionAsync(async x =>
            {
                await reader.GetBulkWriter(x).WriteToServerAsync(cancellationToken, bulkOptions, destinationTable, fields).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

