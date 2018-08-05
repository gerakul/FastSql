using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class DbScope : ICommandCreator
    {
        public DbContext Context { get; }

        public CommandCompilator CommandCompilator
        {
            get
            {
                return Context.CommandCompilator;
            }
        }

        public DbScope(DbContext context)
        {
            this.Context = context;
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

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, paramNames, settings, value, queryOptions);
        }

        #endregion
    }
}
