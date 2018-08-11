namespace Gerakul.FastSql.Common
{
    public class QueryOptions
    {
        public int? CommandTimeoutSeconds { get; set; }

        public QueryOptions(int? commandTimeoutSeconds = null)
        {
            this.CommandTimeoutSeconds = commandTimeoutSeconds;
        }
    }
}
