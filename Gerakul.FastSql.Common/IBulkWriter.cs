using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal interface IBulkWriter
    {
        DbContext Context { get; }

        void WriteToServer(string destinationTable, BulkOptions bulkOptions, params string[] fields);
        Task WriteToServerAsync(string destinationTable, BulkOptions bulkOptions, CancellationToken cancellationToken, params string[] fields);
    }

    internal static class BulkWriterExtensions
    {
        public static Task WriteToServerAsync(this IBulkWriter writer, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return writer.WriteToServerAsync(destinationTable, bulkOptions, CancellationToken.None, fields);
        }

        public static void WriteToServer(this IBulkWriter writer, string destinationTable, params string[] fields)
        {
            writer.WriteToServer(destinationTable, null, fields);
        }

        public static Task WriteToServerAsync(this IBulkWriter writer, string destinationTable, CancellationToken cancellationToken, params string[] fields)
        {
            return writer.WriteToServerAsync(destinationTable, null, cancellationToken, fields);
        }

        public static Task WriteToServerAsync(this IBulkWriter writer, string destinationTable, params string[] fields)
        {
            return writer.WriteToServerAsync(destinationTable, null, CancellationToken.None, fields);
        }
    }
}
