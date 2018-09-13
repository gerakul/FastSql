using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public interface ICommandCreator
    {
        IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter);
    }
}
