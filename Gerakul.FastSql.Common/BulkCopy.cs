using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class BulkCopy
    {
        protected ScopedContext context;

        protected BulkOptions bulkOptions;
        protected string destinationTable;
        protected DbDataReader reader;
        protected string[] fields;

        protected BulkCopy(ScopedContext context, BulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields)
        {
            this.context = context;
            this.bulkOptions = bulkOptions;
            this.destinationTable = destinationTable;
            this.reader = reader;
            this.fields = fields;
        }

        public abstract void WriteToServer();

        public abstract Task WriteToServerAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
