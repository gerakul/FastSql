using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public static class DbCommandExtensions
    {
        // a cancelled enumeration throws, so partial results can't pass for complete ones; the enumeration's token is
        // checked first, a cancelled query token surfaces as the driver's exception
        private static async IAsyncEnumerable<T> QueryAsync<T>(DbCommand cmd, Func<IDataReader, ReadInfo<T>> readInfoGetter,
          CancellationToken queryToken, [EnumeratorCancellation] CancellationToken enumerationToken = default)
        {
            enumerationToken.ThrowIfCancellationRequested();
            using var linkedTokenSource = Helpers.LinkTokens(queryToken, enumerationToken, out var cancellationToken);

            var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var info = readInfoGetter(reader);

            while (true)
            {
                // drivers may skip the token when the next row is already buffered
                cancellationToken.ThrowIfCancellationRequested();
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield break;
                }

                yield return info.GetValue();
            }
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
            return QueryAsync(cmd, r => ReadInfoFactory.CreateObjects(r), cancellationToken);
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
            return QueryAsync(cmd, r => ReadInfoFactory.CreateByType<T>(r, readOptions), cancellationToken);
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
            return QueryAsync(cmd, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, readOptions), cancellationToken);
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
            return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn(r), cancellationToken);
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
            return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn<T>(r), cancellationToken);
        }
    }
}
