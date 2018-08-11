using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

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
            return Set(x => x.CommandCompilator.CompileSimple(commandText).Create(x, queryOptions, parameters));
        }

        #endregion

        #region MappedCommand
        
        internal IWrappedCommand Mapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions)
        {
            return Set(x => precompiledCommand.Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileMapped(commandText, paramNames, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileMapped(commandText, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileMapped<T>(commandText, paramNames, fromTypeOption).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Mapped<T>(string commandText, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileMapped<T>(commandText, fromTypeOption).Create(x, value, queryOptions));
        }

        #region Stored procedures

        internal IWrappedCommand Procedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileProcedure(name, paramNames, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Procedure<T>(string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileProcedure(name, settings).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Procedure<T>(string name, IList<string> paramNames, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileProcedure<T>(name, paramNames, fromTypeOption).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Procedure<T>(string name, T value, FromTypeOption fromTypeOption, QueryOptions queryOptions)
        {
            return Set(x => x.CommandCompilator.CompileProcedure<T>(name, fromTypeOption).Create(x, value, queryOptions));
        }

        #endregion

        #region Special commands

        internal IWrappedCommand Insert<T>(string tableName, T value, QueryOptions queryOptions, bool getIdentity, params string[] ignoreFields)
        {
            return Set(x => x.CommandCompilator.CompileInsert<T>(tableName, getIdentity, ignoreFields).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Update<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileUpdate<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Delete<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileDelete<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        internal IWrappedCommand Merge<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileMerge<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        #endregion

        #endregion

        #endregion
    }
}
