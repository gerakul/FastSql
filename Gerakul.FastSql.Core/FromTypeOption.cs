using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Core
{
    [Flags]
    public enum FromTypeOption : int
    {
        PublicField = 1,
        PublicProperty = 2,

        Default = 3,
        Both = 3,

        Collection = 4,

        All = 7
    }
}
