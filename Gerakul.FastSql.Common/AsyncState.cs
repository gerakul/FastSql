using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal class AsyncState<T>
    {
        public T Current;
        public ReadInfo<T> ReadInfo;
        public DbConnection InternalConnection;
    }
}
