using System;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    internal interface ISetCommandGetter
    {
        IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter);
    }
}
