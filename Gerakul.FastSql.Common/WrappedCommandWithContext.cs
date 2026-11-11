using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal sealed class WrappedCommandWithContext : ICommandCreator, IWrappedCommand
    {
        private readonly ConnectionStringContext context;
        private Func<ScopedContext, DbCommand> commandGetter = null!; // set by Set before a command runs

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

        // the connection lives as long as the enumeration
        private IEnumerable<R> Query<R>(Func<DbCommand, IEnumerable<R>> query)
        {
            using var conn = context.CreateConnection();
            conn.Open();
            var connectionContext = context.ContextProvider.GetConnectionContext(conn, context.QueryOptions, context.BulkOptions, context.ReadOptions);

            foreach (var item in query(commandGetter(connectionContext)))
            {
                yield return item;
            }
        }

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
            return context.UsingConnection(x => commandGetter(x).ExecuteNonQuery());
        }

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            return context.UsingConnectionAsync(x => commandGetter(x).ExecuteNonQueryAsync(cancellationToken), cancellationToken);
        }

        public object? ExecuteScalar()
        {
            return context.UsingConnection(x => commandGetter(x).ExecuteScalar());
        }

        public Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            return context.UsingConnectionAsync(x => commandGetter(x).ExecuteScalarAsync(cancellationToken), cancellationToken);
        }

        public IEnumerable<object[]> ExecuteQuery()
        {
            return Query(x => x.ExecuteQuery());
        }

        public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default)
        {
            return QueryAsync(r => ReadInfoFactory.CreateObjects(r), cancellationToken);
        }

        public IEnumerable<T> ExecuteQuery<T>(ReadOptions? readOptions = null) where T : new()
        {
            return Query(x => x.ExecuteQuery<T>(context.PrepareReadOptions(readOptions)));
        }

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions? readOptions = null, CancellationToken cancellationToken = default) where T : new()
        {
            return QueryAsync(r => ReadInfoFactory.CreateByType<T>(r, context.PrepareReadOptions(readOptions)), cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions? readOptions = null)
        {
            return Query(x => x.ExecuteQueryAnonymous(proto, context.PrepareReadOptions(readOptions)));
        }

        public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions? readOptions = null, CancellationToken cancellationToken = default)
        {
            return QueryAsync(r => ReadInfoFactory.CreateAnonymous(r, proto, context.PrepareReadOptions(readOptions)), cancellationToken);
        }

        public IEnumerable ExecuteQueryFirstColumn()
        {
            return Query(x => x.ExecuteQueryFirstColumn().Cast<object>());
        }

        public IAsyncEnumerable<object?> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default)
        {
            return QueryAsync(r => ReadInfoFactory.CreateFirstColumn(r), cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryFirstColumn<T>()
        {
            return Query(x => x.ExecuteQueryFirstColumn<T>());
        }

        public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default)
        {
            return QueryAsync(r => ReadInfoFactory.CreateFirstColumn<T>(r), cancellationToken);
        }

        public void UseReader(Action<DbDataReader> action)
        {
            context.UsingConnection(x =>
            {
                using var reader = commandGetter(x).ExecuteReader();
                action(reader);
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
