using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql
{
    public static partial class FieldSettings
    {
        public static IEnumerable<ColumnDefinition> GetColumnDefinitions<T>(this IEnumerable<FieldSettings<T>> fieldSettings, ColumnDefinitionOptions options = null)
        {
            foreach (var item in fieldSettings)
            {
                yield return ColumnDefinition.FromFieldType(item.FieldType, item.Name, options);
            }
        }
    }
}
