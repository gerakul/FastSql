using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class DataReaderAttachedToScopedContext : IBulkWriter
    {
        private DbDataReader reader;
        private ScopedContext scopedContext;

        public DbContext Context => scopedContext;

        internal DataReaderAttachedToScopedContext(DbDataReader reader, ScopedContext context) 
        {
            this.reader = reader;
            this.scopedContext = context;
        }

        #region IBulkWriter

        public void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = scopedContext.ContextProvider.GetBulkCopy(scopedContext, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = scopedContext.ContextProvider.GetBulkCopy(scopedContext, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        #endregion
    }
}
