using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;


namespace Gerakul.FastSql
{
    public static partial class DataReaderExtensions
    {
        public static IEnumerable<ColumnDefinition> GetColumnDefinitions(this IDataReader reader, ColumnDefinitionOptions options = null, bool ignoreSchemaTable = false)
        {
            if (reader is DbDataReader && !ignoreSchemaTable)
            {
                var sqlReader = (DbDataReader)reader;
                var schema = sqlReader.GetColumnSchema();
                var optionKeys = options?.PrimaryKey?.Select(x => x.ToLowerInvariant()).ToArray() ?? new string[0];

                foreach (var row in schema)
                {
                    string name = row.ColumnName;
                    yield return new ColumnDefinition()
                    {
                        Name = name,
                        TypeName = row.DataTypeName,
                        IsPrimaryKey = row.IsKey ?? optionKeys.Contains(name.ToLowerInvariant()),
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

        public static DbDataReaderAttachedToContext<DbDataReader, SqlDatabaseContext> AttachContext(this DbDataReader reader)
        {
            return DbDataReaderAttachedToContext.Create(reader, SqlDatabaseContext.Instance);
        }
    }
}
