using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
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
    }

    public static class WrappedCommandExtensions
    {
        public static Task UseReaderAsync(this IWrappedCommand cmd, Func<DbDataReader, Task> action)
        {
            return cmd.UseReaderAsync(CancellationToken.None, action);
        }

        #region WriteToServer

        public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            cmd.UseReader(x => x.WriteToServer(context, destinationTable, bulkOptions, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions bulkOptions, CancellationToken cancellationToken, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, bulkOptions, cancellationToken, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, bulkOptions, fields));
        }

        public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
        {
            cmd.UseReader(x => x.WriteToServer(context, destinationTable, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, cancellationToken, fields));
        }

        public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
        {
            return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, fields));
        }

        #endregion
    }
}
