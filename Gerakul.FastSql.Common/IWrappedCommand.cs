using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public interface IWrappedCommand
    {
        int ExecuteNonQuery();
        Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken));
        object ExecuteScalar();
        Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken));

        IEnumerable<object[]> ExecuteQuery();
        IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null) where T : new();
        IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new();
        IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null);
        IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable ExecuteQueryFirstColumn();
        IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable<T> ExecuteQueryFirstColumn<T>();
        IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken));

        void UseReader(Action<DbDataReader> action);
        Task UseReaderAsync(CancellationToken cancellationToken, Func<DbDataReader, Task> action);
        Task UseReaderAsync(Func<DbDataReader, Task> action);

        DbCommand Unwrap();
        bool TryUnwrap(out DbCommand command);
    }

    public static class WrappedCommandExtensions
    {
        #region WriteToServer

        public static void WriteToServer(this IWrappedCommand cmd, ContextBase context, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            cmd.UseReader(x => x.WriteToServer(context, bulkOptions, destinationTable, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, ContextBase context, CancellationToken cancellationToken, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, cancellationToken, bulkOptions, destinationTable, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, ContextBase context, BulkOptions bulkOptions, string destinationTable, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, bulkOptions, destinationTable, fields));
        }

        public static void WriteToServer(this IWrappedCommand cmd, ContextBase context, string destinationTable, params string[] fields)
        {
            cmd.UseReader(x => x.WriteToServer(context, destinationTable, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, ContextBase context, CancellationToken cancellationToken, string destinationTable, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, cancellationToken, destinationTable, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, ContextBase context, string destinationTable, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, fields));
        }

        #endregion
    }
}
