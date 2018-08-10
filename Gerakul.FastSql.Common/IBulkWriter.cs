using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal interface IBulkWriter
    {
        DbContext Context { get; }

        void WriteToServer(BulkOptions bulkOptions, string destinationTable, params string[] fields);
        Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields);
    }

    internal static class BulkWriterExtensions
    {
        public static Task WriteToServerAsync(this IBulkWriter writer, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            return writer.WriteToServerAsync(CancellationToken.None, bulkOptions, destinationTable, fields);
        }

        public static void WriteToServer(this IBulkWriter writer, string destinationTable, params string[] fields)
        {
            writer.WriteToServer(writer.Context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this IBulkWriter writer, CancellationToken cancellationToken, string destinationTable, params string[] fields)
        {
            return writer.WriteToServerAsync(cancellationToken, writer.Context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this IBulkWriter writer, string destinationTable, params string[] fields)
        {
            return writer.WriteToServerAsync(CancellationToken.None, writer.Context.ContextProvider.DefaultBulkOptions, destinationTable, fields);
        }
    }
}
