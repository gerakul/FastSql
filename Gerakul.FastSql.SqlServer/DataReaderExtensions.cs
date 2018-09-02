using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;


namespace Gerakul.FastSql.SqlServer
{
    // ::: temporary internal
    internal static partial class DataReaderExtensions
    {
        public static IEnumerable<ColumnDefinition> GetColumnDefinitions(this IDataReader reader, ColumnDefinitionOptions options = null, bool ignoreSchemaTable = false)
        {
            if (reader is DbDataReader && !ignoreSchemaTable)
            {
                var sqlReader = (DbDataReader)reader;
                var schema = sqlReader.GetColumnSchema();
                var optionKeys = options?.PrimaryKey?.ToArray() ?? new string[0];

                foreach (var row in schema)
                {
                    string name = row.ColumnName;
                    yield return new ColumnDefinition()
                    {
                        Name = name,
                        TypeName = row.DataTypeName,
                        IsPrimaryKey = row.IsKey ?? optionKeys.Contains(name),
                        MaxLength = checked((short)((row.IsLong ?? false) ? -1 : (row.ColumnSize ?? -1))),
                        Precision = checked((byte)(row.NumericPrecision ?? 0)),
                        Scale = checked((byte)(row.NumericScale ?? 0)),
                        IsNullable = row.AllowDBNull ?? false
                    };
                }
            }
            else
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Type type = reader.GetFieldType(i);
                    string name = reader.GetName(i);
                    yield return ColumnDefinition.FromFieldType(type, name, options);
                }
            }
        }
    }
}
