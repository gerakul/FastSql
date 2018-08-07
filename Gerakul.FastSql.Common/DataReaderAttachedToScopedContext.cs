using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class DataReaderAttachedToScopedContext : BulkWriter
    {
        private DbDataReader reader;
        private ScopedContext ScopedContext => (ScopedContext)context;

        internal DataReaderAttachedToScopedContext(DbDataReader reader, ScopedContext context) 
            : base(context)
        {
            this.reader = reader;
        }

        #region BulkWriter

        public override void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.ContextProvider.GetBulkCopy(ScopedContext, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public override Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.ContextProvider.GetBulkCopy(ScopedContext, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        #endregion
    }
}
