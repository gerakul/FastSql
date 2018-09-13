using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    internal class AsyncState<T>
    {
        public T Current;
        public ReadInfo<T> ReadInfo;
        public DbConnection InternalConnection;
    }
}
