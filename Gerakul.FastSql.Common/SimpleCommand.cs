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
        private DbContext context;

        public string CommandText { get; private set; }

        internal SimpleCommand(DbContext context, string commandText)
        {
            this.context = context;
            this.CommandText = commandText;
        }

        internal DbCommand Create(IDbScope scope, QueryOptions queryOptions, object[] parameters)
        {
            var cmd = scope.CreateCommand(CommandText);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] is DbParameter)
                {
                    cmd.Parameters.Add(parameters[i]);
                }
                else
                {
                    scope.AddParamWithValue(cmd, "@p" + i.ToString(), (object)parameters[i] ?? DBNull.Value);
                }
            }

            context.ApplyQueryOptions(cmd, queryOptions ?? context.DefaultQueryOptions);

            return cmd;
        }
    }
}
