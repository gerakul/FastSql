using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;

namespace Gerakul.FastSql.Common
{
    internal class WrappedCommandWithContext : WCBase, IWrappedCommand
    {
        private DbContext context;
        private Func<DbScope, DbCommand> commandGetter;

        internal WrappedCommandWithContext(DbContext context)
        {
            this.context = context;
        }

        #region WCBase

        public override IWrappedCommand Set(Func<DbScope, DbCommand> commandGetter)
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
                      conn = context.GetConnection();
                      await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                      state.InternalConnection = conn;
                      var scope = new ConnectionScope(context, conn);
                      var reader = await commandGetter(scope).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateByType<T>(r, readOptions));
        }

        #endregion
    }
}
