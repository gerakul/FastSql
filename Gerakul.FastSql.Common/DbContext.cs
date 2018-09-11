using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class DbContext : ISetCommandGetter, ICommandCreator
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

        internal abstract ISetCommandGetter GetISetCommandGetter();

        #region ISetCommandGetter

        public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
        {
            return GetISetCommandGetter().Set(commandGetter);
        }

        #endregion

        #region ICommandCreator

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return Set(x => precompiledCommand.Create(x, queryOptions, parameters));
        }

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return Set(x => x.CommandCompilator.CompileSimple(commandText).Create(x, queryOptions, parameters));
        }

        public IWrappedCommand CreateProcedureSimple(QueryOptions queryOptions, string name, params DbParameter[] parameters)
        {
            return Set(x => x.CommandCompilator.CompileProcedureSimple(name).Create(x, queryOptions, parameters));
        }


        public IWrappedCommand CreateMapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null)
        {
            return Set(x => precompiledCommand.Create(x, value, queryOptions));
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped(commandText, paramNames, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped(commandText, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped<T>(commandText, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateMapped<T>(string commandText, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileMapped<T>(commandText, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure(name, paramNames, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure(name, settings, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure<T>(name, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateProcedure<T>(string name, T value, QueryOptions queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return Set(x =>
            {
                var o = x.PrepareQueryOptions(queryOptions);
                return x.CommandCompilator.CompileProcedure<T>(name, fromTypeOption, o.CaseSensitiveParamsMatching.Value).Create(x, value, o);
            });
        }

        public IWrappedCommand CreateInsert<T>(string tableName, T value, QueryOptions queryOptions, bool getIdentity, params string[] ignoreFields)
        {
            return Set(x => x.CommandCompilator.CompileInsert<T>(tableName, getIdentity, ignoreFields).Create(x, value, queryOptions));
        }

        public IWrappedCommand CreateUpdate<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileUpdate<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        public IWrappedCommand CreateDelete<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileDelete<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        public IWrappedCommand CreateMerge<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return Set(x => x.CommandCompilator.CompileMerge<T>(tableName, keyFields).Create(x, value, queryOptions));
        }

        #endregion
    }
}
