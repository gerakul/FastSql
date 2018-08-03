using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlQueryOptions : QueryOptions
    {
        public SqlQueryOptions(int? commandTimeoutSeconds = null)
        {
            this.CommandTimeoutSeconds = commandTimeoutSeconds;
        }
    }
}
