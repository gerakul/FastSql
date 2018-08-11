using System.Data.Common;
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

        public void WriteToServer(string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            BulkCopy bulk = scopedContext.ContextProvider.GetBulkCopy(scopedContext, reader, destinationTable, bulkOptions, fields);
            bulk.WriteToServer();
        }

        public Task WriteToServerAsync(string destinationTable, BulkOptions bulkOptions, CancellationToken cancellationToken, params string[] fields)
        {
            BulkCopy bulk = scopedContext.ContextProvider.GetBulkCopy(scopedContext, reader, destinationTable, bulkOptions, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        #endregion
    }
}
