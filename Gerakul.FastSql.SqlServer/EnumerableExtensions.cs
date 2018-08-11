using Gerakul.FastSql.Common;
using System.Collections.Generic;

namespace Gerakul.FastSql.SqlServer
{
    // ::: temporary internal
    internal static partial class EnumerableExtensions
    {
        #region Helpers

        public static IEnumerable<ColumnDefinition> GetColumnDefinitions<T>(this IEnumerable<T> values, ColumnDefinitionOptions options = null,
            FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return values.GetFieldSettings(fromTypeOption).GetColumnDefinitions(options);
        }

        public static string GetCreateTableScript<T>(this IEnumerable<T> values, string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions options = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return GetColumnDefinitions(values, options, fromTypeOption).CreateTableScript(tableName, checkIfNotExists);
        }

        #endregion
    }
}
