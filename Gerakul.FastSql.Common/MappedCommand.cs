using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Gerakul.FastSql.Common
{
    public class MappedCommand<T>
    {
        private readonly record struct ParMap(string ParameterName, FieldSettings<T> Settings);

        private readonly ContextProvider contextProvider;
        private readonly ParMap[] map;

        public string CommandText { get; }
        public CommandType CommandType { get; }

        internal MappedCommand(ContextProvider contextProvider, string commandText, IList<string> parameters,
            IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching, CommandType commandType = CommandType.Text)
        {
            this.contextProvider = contextProvider;
            this.CommandText = commandText;
            this.CommandType = commandType;

            // the checks below treat a missing list as an empty one
            parameters ??= Array.Empty<string>();

            var comparer = caseSensitiveParamsMatching ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

            if (parameters.GroupBy(x => contextProvider.GetSqlParameterName(x), comparer).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Parameter names has been duplicated");
            }

            if (settings.GroupBy(x => x.Name, comparer).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Setting names has been duplicated");
            }

            map = parameters
              .Join(settings,
                x => contextProvider.GetSqlParameterName(x),
                x => x.Name,
                (x, y) => new ParMap(x, y),
                comparer).ToArray();

            if (map.Length < parameters.Count)
            {
                throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
            }
        }

        internal DbCommand Create(ScopedContext scopedContext, T value, QueryOptions queryOptions)
        {
            if (scopedContext.ContextProvider != contextProvider)
            {
                throw new ArgumentException($"{nameof(scopedContext.ContextProvider)} presented in received {nameof(ScopedContext)} does not match "
                    + $"stored in given {nameof(MappedCommand<T>)}",
                    nameof(scopedContext));
            }

            var cmd = scopedContext.CreateCommand(CommandText);
            cmd.CommandType = CommandType;

            foreach (var m in map)
            {
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
}
