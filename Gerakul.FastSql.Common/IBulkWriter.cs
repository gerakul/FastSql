using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal interface IBulkWriter
    {
        void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields);
        Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields);
        Task WriteToServerAsync(BulkOptions bulkOptions, string destinationTable, params string[] fields);

        void WriteToServer(string destinationTable, params string[] fields);
        Task WriteToServerAsync(CancellationToken cancellationToken, string destinationTable, params string[] fields);
        Task WriteToServerAsync(string destinationTable, params string[] fields);
    }
}
