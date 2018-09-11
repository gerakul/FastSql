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
            return creator.Set(c =>
            {
                var settings = FieldSettings.FromType<T>(FromTypeOption.Default);
                var fields = settings.Select(x => x.Name).Except(ignoreFields.Select(x => x)).ToArray();
                string query = ((SqlCommandTextGenerator)c.ContextProvider.CommandTextGenerator).InsertAndGetID(tableName, fields);
                return new MappedCommand<T>(c.ContextProvider, query, fields, settings, true).Create(c, value, queryOptions);
            });
        }
    }
}
