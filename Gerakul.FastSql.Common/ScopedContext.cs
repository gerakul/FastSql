using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class ScopedContext : DbContext, ICommandCreator
    {
        internal CommandCompilator CommandCompilator
        {
            get
            {
                return ContextProvider.CommandCompilator;
            }
        }

        public ScopedContext(ContextProvider contextProvider) 
            : base(contextProvider)
        {
        }

        public abstract DbCommand CreateCommand(string commandText);

        private WCBase GetWC()
        {
            return WCBase.Create(this);
        }

        #region ICommandCreator

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return GetWC().Simple(queryOptions, precompiledCommand, parameters);
        }

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return GetWC().Simple(queryOptions, commandText, parameters);
        }


        public IWrappedCommand CreateMapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(precompiledCommand, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, paramNames, settings, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, settings, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, paramNames, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, value, fromTypeOption, queryOptions);
        }


        public IWrappedCommand CreateInsert<T>(QueryOptions queryOptions, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return GetWC().Insert(queryOptions, tableName, value, getIdentity, ignoreFields);
        }

        public IWrappedCommand CreateUpdate<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return GetWC().Update(queryOptions, tableName, value, keyFields);
        }

        public IWrappedCommand CreateMerge<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return GetWC().Merge(queryOptions, tableName, value, keyFields);
        }

        #endregion
    }
}
