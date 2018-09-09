using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class DbContext : ICommandCreator
    {
        public ContextProvider ContextProvider { get; }

        public QueryOptions QueryOptions { get; }
        public BulkOptions BulkOptions { get; }
        public ReadOptions ReadOptions { get; }

        protected internal DbContext(ContextProvider contextProvider,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            this.ContextProvider = contextProvider;
            this.QueryOptions = queryOptions.Clone();
            this.BulkOptions = bulkOptions.Clone();
            this.ReadOptions = readOptions.Clone();
        }

        internal QueryOptions PrepareQueryOptions(QueryOptions options)
        {
            if (options == null)
            {
                return QueryOptions.Clone().SetDefaults();
            }

            var opt = ContextProvider.GetTyped(options, out var needClone);
            if (needClone)
            {
                opt = opt.Clone();
            }

            return opt.SetDefaults(QueryOptions).SetDefaults();
        }

        internal BulkOptions PrepareBulkOptions(BulkOptions options)
        {
            if (options == null)
            {
                return BulkOptions.Clone().SetDefaults();
            }

            var opt = ContextProvider.GetTyped(options, out var needClone);
            if (needClone)
            {
                opt = opt.Clone();
            }

            return opt.SetDefaults(BulkOptions).SetDefaults();
        }

        internal ReadOptions PrepareReadOptions(ReadOptions options)
        {
            if (options == null)
            {
                return ReadOptions.Clone();
            }

            var opt = options.Clone();
            opt.SetDefaults(ReadOptions);
            return opt;
        }

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

        public IWrappedCommand CreateProcedureSimple(QueryOptions queryOptions, string name, params DbParameter[] parameters)
        {
            return GetWC().ProcedureSimple(queryOptions, name, parameters);
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

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return GetWC().Mapped(commandText, paramNames, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return GetWC().Mapped(commandText, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, paramNames, settings, value, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, settings, value, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return GetWC().Procedure(name, paramNames, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return GetWC().Procedure(name, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateInsert<T>(string tableName, T value, QueryOptions queryOptions, bool getIdentity, params string[] ignoreFields)
        {
            return GetWC().Insert(tableName, value, queryOptions, getIdentity, ignoreFields);
        }

        public IWrappedCommand CreateUpdate<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Update(tableName, value, queryOptions, keyFields);
        }

        public IWrappedCommand CreateDelete<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Delete(tableName, value, queryOptions, keyFields);
        }

        public IWrappedCommand CreateMerge<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Merge(tableName, value, queryOptions, keyFields);
        }

        #endregion
    }
}
