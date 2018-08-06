﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public abstract class TransactionContext : ScopedContext
    {
        protected DbTransaction transaction;

        public TransactionContext(ContextProvider contextProvider, DbTransaction transaction) 
            : base(contextProvider)
        {
            this.transaction = transaction;
        }
    }
}
