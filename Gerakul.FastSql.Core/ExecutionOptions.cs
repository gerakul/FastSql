using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public class ExecutionOptions
    {
        public QueryOptions QueryOptions { get; }
        public ReadOptions ReadOptions { get; }

        public ExecutionOptions(QueryOptions queryOptions, ReadOptions readOptions)
        {
            this.QueryOptions = queryOptions;
            this.ReadOptions = readOptions;
        }
    }
}
