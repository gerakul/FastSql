using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public abstract class QueryOptions
    {
        public int? CommandTimeoutSeconds { get; set; }
    }
}
