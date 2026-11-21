using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer;

internal static partial class DataReaderExtensions
{
    // The columns from the reader's column schema when Microsoft.Data.SqlClient made it (its columns are SqlDbColumn,
    // which a wrapper around a SqlDataReader passes on); otherwise, or with ignoreSchemaTable, from the field types.
    // The type names of another database's schema would not be SQL Server's. Null for a field whose type has no
    // column type
    public static IEnumerable<ColumnDefinition?> GetColumnDefinitions(this IDataReader reader, ColumnDefinitionOptions? options = null, bool ignoreSchemaTable = false)
    {
        if (reader is DbDataReader dbReader && !ignoreSchemaTable && HasSchemaTable(reader))
        {
            var schema = dbReader.GetColumnSchema();
            if (schema.All(x => x.GetType().Assembly == typeof(SqlConnection).Assembly))
            {
                var optionKeys = options?.PrimaryKey?.ToArray() ?? [];
                return schema.Select(row => new ColumnDefinition()
                {
                    Name = row.ColumnName,
                    TypeName = row.DataTypeName!,
                    IsPrimaryKey = (row.IsKey ?? false) || optionKeys.Contains(row.ColumnName),
                    MaxLength = checked((short)((row.IsLong ?? false) ? -1 : (row.ColumnSize ?? -1))),
                    Precision = checked((byte)(row.NumericPrecision ?? 0)),
                    Scale = checked((byte)(row.NumericScale ?? 0)),
                    IsNullable = row.AllowDBNull ?? false
                }).ToArray();
            }
        }

        return Enumerable.Range(0, reader.FieldCount).Select(i => ColumnDefinition.FromFieldType(reader.GetFieldType(i), reader.GetName(i), options)).ToArray();
    }

    // UniversalDataReader has no schema table (GetSchemaTable throws); its field types describe the columns
    private static bool HasSchemaTable(IDataReader reader)
    {
        for (var type = reader.GetType(); type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UniversalDataReader<>))
            {
                return false;
            }
        }

        return true;
    }
}
