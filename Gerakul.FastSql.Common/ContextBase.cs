using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class ContextBase
    {
        protected internal readonly ContextProvider ContextProvider;

        public ContextBase(ContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }
    }
}
