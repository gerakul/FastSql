using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    public interface IDbScope
    {
        DbCommand CreateCommand(string commandText);
        DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value);
    }
}
