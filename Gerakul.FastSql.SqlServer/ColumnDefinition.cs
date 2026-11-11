using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Gerakul.FastSql.SqlServer
{
    // ::: temporary internal
    internal sealed class ColumnDefinition
    {
        public required string Name { get; set; }
        public required string TypeName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public short MaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public bool IsNullable { get; set; }

        internal ColumnDefinition()
        {
        }

        [SetsRequiredMembers]
        private ColumnDefinition(string name, string typeName, bool isPrimaryKey, short maxLength, byte precision, byte scale, bool isNullable)
        {
            this.Name = name;
            this.TypeName = typeName;
            this.IsPrimaryKey = isPrimaryKey;
            this.MaxLength = maxLength;
            this.Precision = precision;
            this.Scale = scale;
            this.IsNullable = isNullable;
        }

        public string GetTypeDeclaration()
        {
            return TypeName.ToLowerInvariant() switch
            {
                "time" or "datetime2" or "datetimeoffset" => $"[{TypeName}]({Scale})",
                "decimal" or "numeric" => $"[{TypeName}]({Precision}, {Scale})",
                "binary" or "char" or "nchar" => $"[{TypeName}]({MaxLength})",
                "nvarchar" or "varbinary" or "varchar" => MaxLength == -1 ? $"[{TypeName}](max)" : $"[{TypeName}]({MaxLength})",
                "xml" => throw new NotSupportedException("Type xml is not supported"),
                _ => $"[{TypeName}]"
            };
        }


        public string GetColumnDeclaration()
        {
            return $"[{Name.Replace("]", "]]")}] {GetTypeDeclaration()} {(IsPrimaryKey ? "primary key not null" : (IsNullable ? "null" : "not null"))}";
        }

        // the column types of the supported member types; a Nullable<T> member gets the column type of T
        private static readonly Dictionary<Type, string> columnTypes = new Dictionary<Type, string>()
        {
            [typeof(bool)] = "bit",
            [typeof(byte)] = "tinyint",
            [typeof(short)] = "smallint",
            [typeof(int)] = "int",
            [typeof(long)] = "bigint",
            [typeof(float)] = "real",
            [typeof(double)] = "float",
            [typeof(decimal)] = "decimal",
            [typeof(Guid)] = "uniqueidentifier",
            [typeof(DateTime)] = "datetime2",
            [typeof(string)] = "nvarchar",
            [typeof(byte[])] = "varbinary"
        };

        public static ColumnDefinition? FromFieldType(Type type, string name, ColumnDefinitionOptions? options = null)
        {
            var opt = options ?? ColumnDefinitionOptions.Default;
            bool isPrimaryKey = opt.PrimaryKey?.Contains(name) ?? false;

            var underlyingType = Nullable.GetUnderlyingType(type);
            if (!columnTypes.TryGetValue(underlyingType ?? type, out var typeName))
            {
                if (opt.ThrowIfUnsupportedType)
                {
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported type");
                }

                return null;
            }

            // reference types (string, byte[]) are nullable, value types only as Nullable<T>
            bool isNullable = !type.IsValueType || underlyingType != null;

            return typeName switch
            {
                "decimal" => new ColumnDefinition(name, typeName, isPrimaryKey, 0, opt.Precision, opt.Scale, isNullable),
                "datetime2" => new ColumnDefinition(name, typeName, isPrimaryKey, 0, 0, opt.DateTimeScale, isNullable),
                "nvarchar" => new ColumnDefinition(name, typeName, isPrimaryKey, opt.StringMaxLength, 0, 0, isNullable),
                "varbinary" => new ColumnDefinition(name, typeName, isPrimaryKey, opt.BytesMaxLength, 0, 0, isNullable),
                _ => new ColumnDefinition(name, typeName, isPrimaryKey, 0, 0, 0, isNullable)
            };
        }

    }

    public class ColumnDefinitionOptions
    {
        public List<string>? PrimaryKey { get; set; }
        public short StringMaxLength { get; set; }
        public short BytesMaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public byte DateTimeScale { get; set; }
        public bool ThrowIfUnsupportedType { get; set; } = false;

        public ColumnDefinitionOptions Clone()
        {
            return new ColumnDefinitionOptions()
            {
                PrimaryKey = PrimaryKey == null ? null : new List<string>(PrimaryKey),
                StringMaxLength = StringMaxLength,
                BytesMaxLength = BytesMaxLength,
                Precision = Precision,
                Scale = Scale,
                DateTimeScale = DateTimeScale,
                ThrowIfUnsupportedType = ThrowIfUnsupportedType
            };
        }

        public static readonly ColumnDefinitionOptions Default = new ColumnDefinitionOptions()
        {
            StringMaxLength = 4000,
            BytesMaxLength = 8000,
            Precision = 38,
            Scale = 18,
            DateTimeScale = 7
        };

        public static readonly ColumnDefinitionOptions Max = new ColumnDefinitionOptions()
        {
            StringMaxLength = -1,
            BytesMaxLength = -1,
            Precision = 38,
            Scale = 18,
            DateTimeScale = 7
        };

        public static readonly ColumnDefinitionOptions Small = new ColumnDefinitionOptions()
        {
            StringMaxLength = 255,
            BytesMaxLength = 255,
            Precision = 19,
            Scale = 9,
            DateTimeScale = 3
        };

        public static ColumnDefinitionOptions FromOption(ColumnDefinitionOptions option, params string[] primaryKey)
        {
            var o = option.Clone();
            o.PrimaryKey = primaryKey.ToList();
            return o;
        }

        public static ColumnDefinitionOptions FromDefault(params string[] primaryKey)
        {
            return FromOption(Default, primaryKey);
        }

        public static ColumnDefinitionOptions FromMax(params string[] primaryKey)
        {
            return FromOption(Max, primaryKey);
        }

        public static ColumnDefinitionOptions FromSmall(params string[] primaryKey)
        {
            return FromOption(Small, primaryKey);
        }
    }

    internal static class ColumnDefinitionExtensions
    {
        public static string CreateTableScript(this IEnumerable<ColumnDefinition?> columnDefinitions, string tableName, bool checkIfNotExists = false)
        {
            string create = $@"
create table {tableName}
(
{"\t"} {string.Join(Environment.NewLine + "\t,", columnDefinitions.OfType<ColumnDefinition>().Select(x => x.GetColumnDeclaration()))}
)
";

            if (checkIfNotExists)
            {
                // temp tables live in tempdb, where object_id finds them only with the database name
                var objectName = tableName.StartsWith('#') || tableName.StartsWith("[#", StringComparison.Ordinal) ? "tempdb.." + tableName : tableName;

                return $@"
if object_id (N'{objectName.Replace("'", "''")}', N'U') is null
begin
{create}
end
";
            }
            else
            {
                return create;
            }

        }

        public static IEnumerable<ColumnDefinition?> GetColumnDefinitions<T>(this IEnumerable<FieldSettings<T>> fieldSettings, ColumnDefinitionOptions? options = null)
        {
            foreach (var item in fieldSettings)
            {
                yield return ColumnDefinition.FromFieldType(item.FieldType, item.Name, options);
            }
        }

        public static string CreateTableScript<T>(string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions? options = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return FieldSettings.FromType<T>(fromTypeOption).GetColumnDefinitions(options).CreateTableScript(tableName, checkIfNotExists);
        }

        public static string CreateTableScript<T>(T proto, string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions? options = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return CreateTableScript<T>(tableName, checkIfNotExists, options, fromTypeOption);
        }

    }
}
