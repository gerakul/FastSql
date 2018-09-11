using System;
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

        internal abstract ICommandCreator GetICommandCreator();

        #region ICommandCreator

        public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
        {
            return GetICommandCreator().Set(commandGetter);
        }

        #endregion
    }
}
