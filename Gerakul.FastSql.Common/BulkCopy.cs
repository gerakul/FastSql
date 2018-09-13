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

        protected BulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            this.context = context;
            this.bulkOptions = bulkOptions;
            this.destinationTable = destinationTable;
            this.reader = reader;
            this.fields = fields;
        }

        protected internal abstract void WriteToServer();

        protected internal abstract Task WriteToServerAsync(CancellationToken cancellationToken = default(CancellationToken));

        protected void CheckFieldSelection(FieldsSelector fieldSelector, int sourceNum, int destinationNum, int commonNum)
        {
            Helpers.CheckFieldSelection(fieldSelector, sourceNum, destinationNum, commonNum);
        }
    }
}
