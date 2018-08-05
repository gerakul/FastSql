using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal abstract class WCBase : ISetCommandGetter
    {
        internal static WCBase Create(DbContext context)
        {
            return new WrappedCommandWithContext(context);
        }

        internal static WCBase Create(DbScope scope)
        {
            return new WrappedCommandWithScope(scope);
        }

        public abstract IWrappedCommand Set(Func<DbScope, DbCommand> commandGetter);

        #region Creation

        internal IWrappedCommand Simple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return Set(x => precompiledCommand.Create(x, queryOptions, parameters));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.Compile(commandText, paramNames, settings).Create(x, value, queryOptions));
        }

        #endregion
    }
}
