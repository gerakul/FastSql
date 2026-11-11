using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gerakul.FastSql.SqlServer
{
    public static partial class CommandCreatorExtensions
    {
        public static IWrappedCommand CreateInsertAndGetID<T>(this ISqlCommandCreator creator, string tableName, T value, params string[] ignoreFields)
        {
            return creator.CreateInsertAndGetID(tableName, value, null, ignoreFields);
        }

        public static IWrappedCommand CreateInsertAndGetID<T>(this ISqlCommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
        {
            // the text comes from the context's provider; a creator that is not a context gets the default one
            var provider = (creator as DbContext)?.ContextProvider ?? SqlContextProvider.DefaultInstance;
            var settings = FieldSettings.FromType<T>(FromTypeOption.Default);
            var names = settings.Select(x => x.Name).ToArray();

            // ignored names match members case-insensitively and an exact match wins, as in CommandCompilator
            var ignored = (ignoreFields ?? [])
                .Select(x => names.Contains(x) ? x : names.FirstOrDefault(y => string.Equals(y, x, StringComparison.OrdinalIgnoreCase)) ?? x);
            var fields = names.Except(ignored).ToArray();
            string query = ((SqlCommandTextGenerator)provider.CommandTextGenerator).InsertAndGetID(tableName, fields);
            return creator.CreateMapped(query, fields, settings, value, queryOptions);
        }
    }
}
