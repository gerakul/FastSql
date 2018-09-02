using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Gerakul.FastSql.Common
{
    public class MappedCommand<T>
    {
        private ContextProvider contextProvider;
        private ParMap<T>[] map;

        public string CommandText { get; private set; }
        public CommandType CommandType { get; private set; }

        internal MappedCommand(ContextProvider contextProvider, string commandText, IList<string> parameters, 
            IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching, CommandType commandType = CommandType.Text)
        {
            this.contextProvider = contextProvider;
            this.CommandText = commandText;
            this.CommandType = commandType;

            if (parameters == null || parameters.Count == 0)
            {
                map = new ParMap<T>[0];
            }

            if (caseSensitiveParamsMatching)
            {
                if (parameters.GroupBy(x => contextProvider.GetSqlParameterName(x)).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Parameter names has been duplicated");
                }

                if (settings.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Setting names has been duplicated");
                }

                map = parameters
                  .Join(settings,
                    x => contextProvider.GetSqlParameterName(x),
                    x => x.Name,
                    (x, y) => new ParMap<T>() { ParameterName = x, Settings = y }).ToArray();
            }
            else
            {
                if (parameters.GroupBy(x => contextProvider.GetSqlParameterName(x).ToLowerInvariant()).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Parameter names has been duplicated");
                }

                if (settings.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Setting names has been duplicated");
                }

                map = parameters
                  .Join(settings,
                    x => contextProvider.GetSqlParameterName(x).ToLowerInvariant(),
                    x => x.Name.ToLowerInvariant(),
                    (x, y) => new ParMap<T>() { ParameterName = x, Settings = y }).ToArray();
            }

            if (map.Length < parameters.Count)
            {
                throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
            }
        }

        internal DbCommand Create(ScopedContext scopedContext, T value, QueryOptions queryOptions)
        {
            var cmd = scopedContext.CreateCommand(CommandText);
            cmd.CommandType = CommandType;

            for (int i = 0; i < map.Length; i++)
            {
                var m = map[i];
                object paramValue;
                if (m.Settings.DetermineNullByValue)
                {
                    var val = m.Settings.Getter(value);
                    paramValue = val == null ? contextProvider.GetDbNull(m.Settings.FieldType) : val;
                }
                else
                {
                    paramValue = m.Settings.IsNullGetter(value) ? contextProvider.GetDbNull(m.Settings.FieldType) : m.Settings.Getter(value);
                }

                if (paramValue is DbParameter)
                {
                    cmd.Parameters.Add(paramValue);
                }
                else
                {
                    contextProvider.AddParamWithValue(cmd, m.ParameterName, paramValue);
                }
            }

            contextProvider.ApplyQueryOptions(cmd, scopedContext.PrepareQueryOptions(queryOptions));

            return cmd;
        }
    }

    internal class ParMap<T>
    {
        public string ParameterName;
        public FieldSettings<T> Settings;
    }
}
