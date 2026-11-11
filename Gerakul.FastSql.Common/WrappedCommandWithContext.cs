using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class WrappedCommandWithContext : ICommandCreator, IWrappedCommand
    {
        private ConnectionStringContext context;
        private Func<ScopedContext, DbCommand> commandGetter;

        internal WrappedCommandWithContext(ConnectionStringContext context)
        {
            this.context = context;
        }

        #region ICommandCreator

        public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
        {
            this.commandGetter = commandGetter;
            return this;
        }

        #endregion

        // the connection lives as long as the enumeration; a cancelled enumeration throws, so partial results can't pass
        // for complete ones. The enumeration's token is checked first, so a cancelled one opens no connection
        private async IAsyncEnumerable<R> QueryAsync<R>(Func<DbDataReader, ReadInfo<R>> readInfoGetter,
          CancellationToken queryToken, [EnumeratorCancellation] CancellationToken enumerationToken = default)
        {
            enumerationToken.ThrowIfCancellationRequested();
            using var linkedTokenSource = Helpers.LinkTokens(queryToken, enumerationToken, out var cancellationToken);

            var conn = context.CreateConnection();
            await using var connectionScope = conn.ConfigureAwait(false);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);

            var reader = await commandGetter(connectionContext).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        #region IWrappedCommand

        public int ExecuteNonQuery()
        {
            int result = default(int);
            context.UsingConnection(x =>
            {
                result = commandGetter(x).ExecuteNonQuery();
            });

            return result;
        }

        public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            int result = default(int);
            await context.UsingConnectionAsync(async x =>
            {
                result = await commandGetter(x).ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        public object ExecuteScalar()
        {
            object result = default(object);
            context.UsingConnection(x =>
            {
                result = commandGetter(x).ExecuteScalar();
            });

            return result;
        }

        public async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            object result = default(object);
            await context.UsingConnectionAsync(async x =>
            {
                result = await commandGetter(x).ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        public IEnumerable<object[]> ExecuteQuery()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);
                foreach (var item in commandGetter(connectionContext).ExecuteQuery())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return QueryAsync(r => ReadInfoFactory.CreateObjects(r), cancellationToken);
        }

        public IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null) where T : new()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);
                foreach (var item in commandGetter(connectionContext).ExecuteQuery<T>(context.PrepareReadOptions(readOptions)))
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return QueryAsync(r => ReadInfoFactory.CreateByType<T>(r, context.PrepareReadOptions(readOptions)), cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null)
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);
                foreach (var item in commandGetter(connectionContext)
                    .ExecuteQueryAnonymous<T>(proto, context.PrepareReadOptions(readOptions)))
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return QueryAsync(r => ReadInfoFactory.CreateAnonymous(r, proto, context.PrepareReadOptions(readOptions)), cancellationToken);
        }

        public IEnumerable ExecuteQueryFirstColumn()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);
                foreach (var item in commandGetter(connectionContext).ExecuteQueryFirstColumn())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return QueryAsync(r => ReadInfoFactory.CreateFirstColumn(r), cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryFirstColumn<T>()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);
                foreach (var item in commandGetter(connectionContext).ExecuteQueryFirstColumn<T>())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken))
        {
            return QueryAsync(r => ReadInfoFactory.CreateFirstColumn<T>(r), cancellationToken);
        }

        public void UseReader(Action<DbDataReader> action)
        {
            context.UsingConnection(x =>
            {
                using (var reader = commandGetter(x).ExecuteReader())
                {
                    action(reader);
                }
            });
        }

        public async Task UseReaderAsync(CancellationToken cancellationToken, Func<DbDataReader, Task> action)
        {
            await context.UsingConnectionAsync(async x =>
            {
                var reader = await commandGetter(x).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using var readerScope = reader.ConfigureAwait(false);
                await action(reader).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
