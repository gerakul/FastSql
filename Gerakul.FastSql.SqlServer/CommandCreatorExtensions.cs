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

        public static IWrappedCommand CreateInsertAndGetID<T>(this ISqlCommandCreator creator, string tableName, T value, QueryOptions queryOptions, params string[] ignoreFields)
        {
            // the text comes from the context's provider; a creator that is not a context gets the default one
            var provider = (creator as DbContext)?.ContextProvider ?? SqlContextProvider.DefaultInstance;
            var settings = FieldSettings.FromType<T>(FromTypeOption.Default);
            var fields = settings.Select(x => x.Name).Except(ignoreFields ?? new string[0]).ToArray();
            string query = ((SqlCommandTextGenerator)provider.CommandTextGenerator).InsertAndGetID(tableName, fields);
            return creator.CreateMapped(query, fields, settings, value, queryOptions);
        }
    }
}
