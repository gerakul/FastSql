namespace Gerakul.FastSql.Common
{
    public abstract class DbContext
    {
        protected internal readonly ContextProvider ContextProvider;

        public DbContext(ContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }
    }
}
