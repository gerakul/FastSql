using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal abstract class BulkWriter : IBulkWriter
    {
        protected DbContext context;

        internal BulkWriter(DbContext context)
        {
            this.context = context;
        }

        public abstract void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields);
        public abstract Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields);

        public Task WriteToServerAsync(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, destinationTable, fields);
        }

        public void WriteToServer(string destinationTable, params string[] fields)
        {
            WriteToServer(context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(cancellationToken, context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }

        public Task WriteToServerAsync(string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }
    }
}
