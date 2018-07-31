using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
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
            return ReadAll<T>(reader, new ReadOptions());
        }

        public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader) where T : new()
        {
            return ReadAllAsync<T>(reader, new ReadOptions());
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
            return ReadAllAnonymous(reader, proto, new ReadOptions());
        }

        public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto)
        {
            return ReadAllAnonymousAsync(reader, proto, new ReadOptions());
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
