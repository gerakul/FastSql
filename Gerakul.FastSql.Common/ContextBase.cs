using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class ContextBase
    {
        internal readonly ContextProvider ContextProvider;

        public ContextBase(ContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }
    }
}
