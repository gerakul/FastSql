using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class EnumerableAttachedToContext<T> : BulkWriter
    {
        private IEnumerable<T> enumerable;

        internal EnumerableAttachedToContext(IEnumerable<T> enumerable, ContextBase context)
            : base(context)
        {
            this.enumerable = enumerable;
        }

        #region BulkWriter

        public override void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            enumerable.ToDataReader().AttachContext(context).WriteToServer(bulkOptions, destinationTable, fields);
        }

        public override Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            return enumerable.ToDataReader().AttachContext(context).WriteToServerAsync(cancellationToken, bulkOptions, destinationTable, fields);
        }

        #endregion
    }
}
