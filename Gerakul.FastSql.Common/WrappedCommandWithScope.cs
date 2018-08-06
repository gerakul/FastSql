using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class WrappedCommandWithScope : WCBase, IWrappedCommand
    {
        private DbScope scope;
        private DbCommand command;

        internal WrappedCommandWithScope(DbScope scope)
        {
            this.scope = scope;
        }

        #region WCBase

        public override IWrappedCommand Set(Func<DbScope, DbCommand> commandGetter)
        {
            command = commandGetter(scope);
            return this;
        }

        #endregion

        #region IWrappedCommand

        public int ExecuteNonQuery()
        {
            return command.ExecuteNonQuery();
        }

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteNonQueryAsync(cancellationToken);
        }

        public object ExecuteScalar()
        {
            return command.ExecuteScalar();
        }

        public Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteScalarAsync(cancellationToken);
        }


        public IEnumerable<object[]> ExecuteQuery()
        {
            return command.ExecuteQuery();
        }

        public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteQueryAsync(cancellationToken);
        }

        public IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null) where T : new()
        {
            return command.ExecuteQuery<T>(readOptions);
        }

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return command.ExecuteQueryAsync<T>(readOptions, cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null)
        {
            return command.ExecuteQueryAnonymous(proto, readOptions);
        }

        public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteQueryAnonymousAsync(proto, readOptions, cancellationToken);
        }

        public IEnumerable ExecuteQueryFirstColumn()
        {
            return command.ExecuteQueryFirstColumn();
        }

        public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteQueryFirstColumnAsync(cancellationToken);
        }

        public IEnumerable<T> ExecuteQueryFirstColumn<T>()
        {
            return command.ExecuteQueryFirstColumn<T>();
        }

        public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken))
        {
            return command.ExecuteQueryFirstColumnAsync<T>(cancellationToken);
        }

        #endregion
    }
}
