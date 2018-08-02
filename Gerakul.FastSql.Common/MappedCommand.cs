using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public class MappedCommand<T>
    {
        public string CommandText { get; private set; }
        private ParMap<T>[] map;

        internal MappedCommand(string commandText, IList<string> parameters, IList<FieldSettings<T>> settings)
        {
            this.CommandText = commandText;

            if (parameters == null || parameters.Count == 0)
            {
                map = new ParMap<T>[0];
            }

            if (parameters.GroupBy(x => x.GetSqlParameterName().ToLowerInvariant()).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Parameter names has been duplicated");
            }

            if (settings.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
            {
                throw new ArgumentException("Setting names has been duplicated");
            }

            map = parameters
              .Join(settings,
                x => x.GetSqlParameterName().ToLowerInvariant(),
                x => x.Name.ToLowerInvariant(),
                (x, y) => new ParMap<T>() { ParameterName = x, Settings = y }).ToArray();

            if (map.Length < parameters.Count)
            {
                throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
            }
        }

        internal DbCommand Create(IDbScope scope, T value, QueryOptions queryOptions = null)
        {
            var cmd = scope.CreateCommand(CommandText);

            for (int i = 0; i < map.Length; i++)
            {
                var m = map[i];
                object paramValue;
                if (m.Settings.DetermineNullByValue)
                {
                    var val = m.Settings.Getter(value);
                    paramValue = val == null ? DBNull.Value : val;
                }
                else
                {
                    paramValue = m.Settings.IsNullGetter(value) ? DBNull.Value : m.Settings.Getter(value);
                }

                if (m.Settings.FieldType.Equals(typeof(byte[])) && paramValue == DBNull.Value)
                {
                    paramValue = System.Data.SqlTypes.SqlBytes.Null;
                }

                scope.AddParamWithValue(cmd, m.ParameterName, paramValue);
            }

            Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());

            return cmd;
        }
    }

    internal class ParMap<T>
    {
        public string ParameterName;
        public FieldSettings<T> Settings;
    }
}
