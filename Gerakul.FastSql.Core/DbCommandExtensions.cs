using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public static class DbCommandExtensions
    {
        private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(DbCommand cmd, CancellationToken cancellationToken,
          Func<IDataReader, ReadInfo<T>> readInfoGetter)
        {
            return Helpers.CreateAsyncEnumerable<T>(
              state =>
              {
                  state?.ReadInfo?.Reader?.Dispose();
              },

              async (state, ct) =>
              {
                  var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                  state.ReadInfo = readInfoGetter(reader);
              });
        }

        public static IEnumerable<object[]> ExecuteQuery(this DbCommand cmd)
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                foreach (var item in reader.ReadAll())
                {
                    yield return item;
                }
            }
        }

        public static IAsyncEnumerable<object[]> ExecuteQueryAsync(this DbCommand cmd, CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cmd, cancellationToken, r => ReadInfoFactory.CreateObjects(r));
        }

        public static IEnumerable<T> ExecuteQuery<T>(this DbCommand cmd, ReadOptions readOptions = null) where T : new()
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                foreach (var item in reader.ReadAll<T>(readOptions))
                {
                    yield return item;
                }
            }
        }

        public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(this DbCommand cmd, ReadOptions readOptions = null,
          CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return CreateAsyncEnumerable(cmd, cancellationToken, r => ReadInfoFactory.CreateByType<T>(r, readOptions));
        }

        public static IEnumerable<T> ExecuteQueryAnonymous<T>(this DbCommand cmd, T proto, ReadOptions readOptions = null)
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                foreach (var item in reader.ReadAllAnonymous(proto, readOptions))
                {
                    yield return item;
                }
            }
        }

        public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(this DbCommand cmd, T proto, ReadOptions readOptions = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cmd, cancellationToken, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, readOptions));
        }

        public static IEnumerable ExecuteQueryFirstColumn(this DbCommand cmd)
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                foreach (var item in reader.ReadAllFirstColumn())
                {
                    yield return item;
                }
            }
        }

        public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(this DbCommand cmd,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cmd, cancellationToken, r => ReadInfoFactory.CreateFirstColumn(r));
        }

        public static IEnumerable<T> ExecuteQueryFirstColumn<T>(this DbCommand cmd)
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                foreach (var item in reader.ReadAllFirstColumn<T>())
                {
                    yield return item;
                }
            }
        }

        public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(this DbCommand cmd,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cmd, cancellationToken, r => ReadInfoFactory.CreateFirstColumn<T>(r));
        }
    }
}
