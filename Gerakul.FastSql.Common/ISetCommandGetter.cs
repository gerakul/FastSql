using System;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public interface ISetCommandGetter
    {
        IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter);
    }
}
