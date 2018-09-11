using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class ScopedContext : DbContext
    {
        internal CommandCompilator CommandCompilator
        {
            get
            {
                return ContextProvider.CommandCompilator;
            }
        }

        protected internal ScopedContext(ContextProvider contextProvider,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, queryOptions, bulkOptions, readOptions)
        {
        }

        internal override ICommandCreator GetICommandCreator()
        {
            return new WrappedCommandWithScope(this);
        }

        public abstract DbCommand CreateCommand(string commandText);
    }
}
