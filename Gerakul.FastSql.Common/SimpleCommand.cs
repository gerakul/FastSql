using System;
using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common
{
    public class SimpleCommand
    {
        private ContextProvider contextProvider;

        public string CommandText { get; private set; }
        public CommandType CommandType { get; private set; }

        internal SimpleCommand(ContextProvider contextProvider, string commandText, 
            CommandType commandType = CommandType.Text)
        {
            this.contextProvider = contextProvider;
            this.CommandText = commandText;
            this.CommandType = commandType;
        }

        internal DbCommand Create(ScopedContext scopedContext, QueryOptions queryOptions, object[] parameters)
        {
            if (scopedContext.ContextProvider != contextProvider)
            {
                throw new ArgumentException($"{nameof(scopedContext.ContextProvider)} presented in received {nameof(ScopedContext)} does not match "
                    + $"stored in given {nameof(SimpleCommand)}",
                    nameof(scopedContext));
            }

            var cmd = scopedContext.CreateCommand(CommandText);
            cmd.CommandType = CommandType;

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

            contextProvider.ApplyQueryOptions(cmd, scopedContext.PrepareQueryOptions(queryOptions));

            return cmd;
        }
    }
}
