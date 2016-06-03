using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  internal class AsyncState<T>
  {
    public T Current;
    public ReadInfo<T> ReadInfo;
    public SqlConnection InternalConnection;
  }
}
