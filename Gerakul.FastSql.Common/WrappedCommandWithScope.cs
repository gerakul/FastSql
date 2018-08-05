using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;

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

        public IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return command.ExecuteQueryAsync<T>(readOptions, cancellationToken);
        }

        #endregion
    }
}
