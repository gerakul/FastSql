using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    internal interface ISetCommandGetter
    {
        IWrappedCommand Set(Func<DbScope, DbCommand> commandGetter);
    }
}
