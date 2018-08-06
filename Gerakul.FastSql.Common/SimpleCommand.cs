using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public class SimpleCommand
    {
        private ContextProvider contextProvider;

        public string CommandText { get; private set; }

        internal SimpleCommand(ContextProvider contextProvider, string commandText)
        {
            this.contextProvider = contextProvider;
            this.CommandText = commandText;
        }

        internal DbCommand Create(ScopedContext scopedContext, QueryOptions queryOptions, object[] parameters)
        {
            var cmd = scopedContext.CreateCommand(CommandText);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] is DbParameter)
                {
                    cmd.Parameters.Add(parameters[i]);
                }
                else
                {
                    contextProvider.AddParamWithValue(cmd, "@p" + i.ToString(), (object)parameters[i] ?? DBNull.Value);
                }
            }

            contextProvider.ApplyQueryOptions(cmd, queryOptions ?? contextProvider.DefaultQueryOptions);

            return cmd;
        }
    }
}
