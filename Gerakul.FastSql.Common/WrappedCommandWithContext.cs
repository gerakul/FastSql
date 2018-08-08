using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class WrappedCommandWithContext : WCBase, IWrappedCommand
    {
        private DbContext context;
        private Func<ScopedContext, DbCommand> commandGetter;

        internal WrappedCommandWithContext(DbContext context)
        {
            this.context = context;
        }

        #region WCBase

        public override IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
        {
            this.commandGetter = commandGetter;
            return this;
        }

        #endregion

        private AEnumerable<AsyncState<R>, R> CreateAsyncEnumerable<R>(CancellationToken cancellationToken,
          Func<DbDataReader, ReadInfo<R>> readInfoGetter)
        {
            return Helpers.CreateAsyncEnumerable<R>(
              state =>
              {
                  state?.ReadInfo?.Reader?.Dispose();
                  state?.InternalConnection?.Dispose();
              },

              async (state, ct) =>
              {
                  DbConnection conn = null;
                  try
                  {
                      conn = context.CreateConnection();
                      await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                      state.InternalConnection = conn;
                      var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                      var reader = await commandGetter(connectionContext).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                      state.ReadInfo = readInfoGetter(reader);
                  }
                  catch
                  {
                      if (conn != null)
                      {
                          conn.Close();
                      }

                      throw;
                  }
              });
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
                result = await commandGetter(x).ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

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
            }).ConfigureAwait(false);

            return result;
        }

        public IEnumerable<object[]> ExecuteQuery()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                foreach (var item in commandGetter(connectionContext).ExecuteQuery())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateObjects(r));
        }

        public IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null) where T : new()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                foreach (var item in commandGetter(connectionContext).ExecuteQuery<T>(readOptions))
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateByType<T>(r, readOptions));
        }

        public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null)
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                foreach (var item in commandGetter(connectionContext).ExecuteQueryAnonymous<T>(proto, readOptions))
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateAnonymous(r, proto, readOptions));
        }

        public IEnumerable ExecuteQueryFirstColumn()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                foreach (var item in commandGetter(connectionContext).ExecuteQueryFirstColumn())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn(r));
        }

        public IEnumerable<T> ExecuteQueryFirstColumn<T>()
        {
            using (var conn = context.CreateConnection())
            {
                conn.Open();
                var connectionContext = context.ContextProvider.CreateConnectionContext(conn);
                foreach (var item in commandGetter(connectionContext).ExecuteQueryFirstColumn<T>())
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn<T>(r));
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
                using (var reader = await commandGetter(x).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    await action(reader).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        public Task UseReaderAsync(Func<DbDataReader, Task> action)
        {
            return UseReaderAsync(CancellationToken.None, action);
        }

        public DbCommand Unwrap()
        {
            throw new InvalidOperationException($"Try to unwrap command outside the {nameof(ScopedContext)}");
        }

        public bool TryUnwrap(out DbCommand command)
        {
            command = null;
            return false;
        }

        #endregion
    }
}
