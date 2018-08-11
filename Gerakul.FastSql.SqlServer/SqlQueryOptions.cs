using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlQueryOptions : QueryOptions
    {
        public SqlQueryOptions(int? commandTimeoutSeconds = null)
            : base(commandTimeoutSeconds)
        {
        }
    }
}
