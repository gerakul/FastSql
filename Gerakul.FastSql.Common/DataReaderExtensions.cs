﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public static partial class DataReaderExtensions
    {
        #region Read

        private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(Func<ReadInfo<T>> readInfoGetter)
        {
            return Helpers.CreateAsyncEnumerable<T>(null,
              (state, ct) =>
              {
                  state.ReadInfo = readInfoGetter();
                  return Task.CompletedTask;
              });
        }

        private static IEnumerable<T> CreateEnumerable<T>(Func<ReadInfo<T>> readInfoGetter)
        {
            var info = readInfoGetter();
            IDataReader reader = info.Reader;

            while (reader.Read())
            {
                yield return info.GetValue();
            }
        }

        internal static Task<bool> ReadAsyncInternal(this IDataReader reader, CancellationToken cancellationToken)
        {
            if (reader is DbDataReader)
            {
                return ((DbDataReader)reader).ReadAsync(cancellationToken);
            }

            return Task.Run(() => reader.Read(), cancellationToken);
        }

        public static IEnumerable<object[]> ReadAll(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateObjects(reader));
        }

        public static IAsyncEnumerable<object[]> ReadAllAsync(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateObjects(reader));
        }

        public static IEnumerable<T> ReadAll<T>(this IDataReader reader, ReadOptions readOptions) where T : new()
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateByType<T>(reader, readOptions));
        }

        public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader, ReadOptions readOptions) where T : new()
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateByType<T>(reader, readOptions));
        }

        public static IEnumerable<T> ReadAll<T>(this IDataReader reader) where T : new()
        {
            return ReadAll<T>(reader, null);
        }

        public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader) where T : new()
        {
            return ReadAllAsync<T>(reader, null);
        }

        public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto, ReadOptions readOptions)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
        }

        public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto, ReadOptions readOptions)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
        }

        public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto)
        {
            return ReadAllAnonymous(reader, proto, null);
        }

        public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto)
        {
            return ReadAllAnonymousAsync(reader, proto, null);
        }

        public static IEnumerable ReadAllFirstColumn(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
        }

        public static IAsyncEnumerable<object> ReadAllFirstColumnAsync(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
        }

        public static IEnumerable<T> ReadAllFirstColumn<T>(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
        }

        public static IAsyncEnumerable<T> ReadAllFirstColumnAsync<T>(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
        }

        #endregion

        #region WriteToServer

        internal static IBulkWriter GetBulkWriter(this DbDataReader reader, DbContext context)
        {
            if (context is ScopedContext)
            {
                return new DataReaderAttachedToScopedContext(reader, (ScopedContext)context);
            }
            else if (context is ConnectionStringContext)
            {
                return new DataReaderAttachedToConnectionStringContext(reader, (ConnectionStringContext)context);
            }
            else
            {
                throw new ArgumentException(@"Unknown type of context", nameof(context));
            }
        }

        public static void WriteToServer(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            reader.GetBulkWriter(context).WriteToServer(destinationTable, bulkOptions, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions bulkOptions, CancellationToken cancellationToken, params string[] fields)
        {
            return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, bulkOptions, cancellationToken, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, bulkOptions, fields);
        }

        public static void WriteToServer(this DbDataReader reader, DbContext context, string destinationTable, params string[] fields)
        {
            reader.GetBulkWriter(context).WriteToServer(destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
        {
            return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, cancellationToken, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, params string[] fields)
        {
            return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, fields);
        }

        #endregion

        public static IEnumerable<string> GetColumnNames(this IDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string name = reader.GetName(i);
                yield return name;
            }
        }
    }
}
