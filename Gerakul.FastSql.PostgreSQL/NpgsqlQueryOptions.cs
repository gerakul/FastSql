using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlQueryOptions : QueryOptions
    {
        public NpgsqlQueryOptions(int? commandTimeoutSeconds = null)
            : base(commandTimeoutSeconds)
        {
        }
    }
}
