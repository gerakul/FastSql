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
        internal static WCBase Create(ConnectionStringContext context)
        {
            return new WrappedCommandWithContext(context);
        }

        internal static WCBase Create(ScopedContext context)
        {
            return new WrappedCommandWithScope(context);
        }

        public abstract IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter);

        #region Creation

        #region SimpleCommand

        internal IWrappedCommand Simple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return Set(x => precompiledCommand.Create(x, queryOptions, parameters));
        }

        internal IWrappedCommand Simple(QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return Set(x => x.CommandCompilator.Compile(commandText).Create(x, queryOptions, parameters));
        }

        #endregion

        #region MappedCommand
        
        internal IWrappedCommand Mapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions)
        {
            return Set(x => precompiledCommand.Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.Compile(commandText, paramNames, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.Compile(commandText, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.Compile<T>(commandText, paramNames, fromTypeOption).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.Compile<T>(commandText, fromTypeOption).Create(x, value, queryOptions));
        }

        #region Special commands

        internal IWrappedCommand Insert<T>(QueryOptions queryOptions, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return Set(x => x.CommandCompilator.CompileInsert<T>(tableName, getIdentity, ignoreFields).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Update<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileUpdate<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Merge<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileMerge<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        #endregion

        #endregion

        #endregion
    }
}
