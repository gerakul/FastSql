using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public abstract class TransactionContext : ScopedContext
    {
        public DbTransaction Transaction { get; }

        public TransactionContext(ContextProvider contextProvider, DbTransaction transaction) 
            : base(contextProvider)
        {
            this.Transaction = transaction;
        }
    }
}
