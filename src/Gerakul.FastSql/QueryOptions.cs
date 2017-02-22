using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
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
