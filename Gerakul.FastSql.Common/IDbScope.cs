using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public interface IDbScope : ICommandCreator
    {
        DbCommand CreateCommand(string commandText);
        DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value);
    }
}
