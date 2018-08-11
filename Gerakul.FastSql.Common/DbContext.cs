namespace Gerakul.FastSql.Common
{
    public abstract class DbContext
    {
        public ContextProvider ContextProvider { get; }

        public DbContext(ContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }
    }
}
