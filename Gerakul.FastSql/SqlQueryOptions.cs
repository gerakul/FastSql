using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql
{
    public class SqlQueryOptions : QueryOptions
    {
        public SqlQueryOptions(int? commandTimeoutSeconds = null)
        {
            this.CommandTimeoutSeconds = commandTimeoutSeconds;
        }
    }
}
