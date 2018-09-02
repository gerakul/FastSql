using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gerakul.FastSql.PostgreSQL
{
    // ::: temporary internal
    internal class ColumnDefinition
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public short MaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public bool IsNullable { get; set; }

        internal ColumnDefinition()
        {
        }

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
            switch (TypeName.ToLowerInvariant())
            {
                case "time":
                case "datetime2":
                case "datetimeoffset":
                    return $"[{TypeName}]({Scale})";
                case "decimal":
                case "numeric":
                    return $"[{TypeName}]({Precision}, {Scale})";
                case "binary":
                case "char":
                case "nchar":
                    return $"[{TypeName}]({MaxLength})";
                case "nvarchar":
                    return MaxLength == -1 ? $"[{TypeName}](max)" : $"[{TypeName}]({MaxLength})";
                case "varbinary":
                case "varchar":
                    return MaxLength == -1 ? $"[{TypeName}](max)" : $"[{TypeName}]({MaxLength})";
                case "xml":
                    throw new NotSupportedException("Type xml is not supported");
                default:
                    return $"[{TypeName}]";
            }
        }

        public string GetColumnDeclaration()
        {
            return $"[{Name}] {GetTypeDeclaration()} {(IsPrimaryKey ? "primary key not null" : (IsNullable ? "null" : "not null"))}";
        }

        public static ColumnDefinition FromFieldType(Type type, string name, ColumnDefinitionOptions options = null)
        {
            var opt = options ?? ColumnDefinitionOptions.Default;
            bool isPrimaryKey = opt.PrimaryKey?.Contains(name) ?? false;

            if (type.Equals(typeof(bool)))
            {
                return new ColumnDefinition(name, "bit", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(bool?)))
            {
                return new ColumnDefinition(name, "bit", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(byte)))
            {
                return new ColumnDefinition(name, "tinyint", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(byte?)))
            {
                return new ColumnDefinition(name, "tinyint", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(short)))
            {
                return new ColumnDefinition(name, "smallint", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(short?)))
            {
                return new ColumnDefinition(name, "smallint", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(int)))
            {
                return new ColumnDefinition(name, "int", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(int?)))
            {
                return new ColumnDefinition(name, "int", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(long)))
            {
                return new ColumnDefinition(name, "bigint", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(long?)))
            {
                return new ColumnDefinition(name, "bigint", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(float)))
            {
                return new ColumnDefinition(name, "real", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(float?)))
            {
                return new ColumnDefinition(name, "real", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(double)))
            {
                return new ColumnDefinition(name, "float", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(double?)))
            {
                return new ColumnDefinition(name, "float", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(decimal)))
            {
                return new ColumnDefinition(name, "decimal", isPrimaryKey, 0, opt.Precision, opt.Scale, false);
            }
            else if (type.Equals(typeof(decimal?)))
            {
                return new ColumnDefinition(name, "decimal", isPrimaryKey, 0, opt.Precision, opt.Scale, true);
            }
            if (type.Equals(typeof(Guid)))
            {
                return new ColumnDefinition(name, "uniqueidentifier", isPrimaryKey, 0, 0, 0, false);
            }
            else if (type.Equals(typeof(Guid?)))
            {
                return new ColumnDefinition(name, "uniqueidentifier", isPrimaryKey, 0, 0, 0, true);
            }
            if (type.Equals(typeof(DateTime)))
            {
                return new ColumnDefinition(name, "datetime2", isPrimaryKey, 0, 0, opt.DateTimeScale, false);
            }
            else if (type.Equals(typeof(DateTime?)))
            {
                return new ColumnDefinition(name, "datetime2", isPrimaryKey, 0, 0, opt.DateTimeScale, true);
            }
            if (type.Equals(typeof(string)))
            {
                return new ColumnDefinition(name, "nvarchar", isPrimaryKey, opt.StringMaxLength, 0, 0, true);
            }
            if (type.Equals(typeof(byte[])))
            {
                return new ColumnDefinition(name, "varbinary", isPrimaryKey, opt.BytesMaxLength, 0, 0, true);
            }

            if (opt.ThrowIfUnsupportedType)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported type");
            }

            return null;
        }
    }

    public class ColumnDefinitionOptions
    {
        public List<string> PrimaryKey { get; set; }
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
        public static string CreateTableScript(this IEnumerable<ColumnDefinition> columnDefinitions, string tableName, bool checkIfNotExists = false)
        {
            string create = $@"
create table {tableName}
(
{"\t"} {string.Join(Environment.NewLine + "\t,", columnDefinitions.Where(x => x != null).Select(x => x.GetColumnDeclaration()))}
)
";

            if (checkIfNotExists)
            {
                return $@"
if object_id (N'{tableName}', N'U') is null
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

        public static IEnumerable<ColumnDefinition> GetColumnDefinitions<T>(this IEnumerable<FieldSettings<T>> fieldSettings, ColumnDefinitionOptions options = null)
        {
            foreach (var item in fieldSettings)
            {
                yield return ColumnDefinition.FromFieldType(item.FieldType, item.Name, options);
            }
        }

        public static string CreateTableScript<T>(string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions options = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return FieldSettings.FromType<T>(fromTypeOption).GetColumnDefinitions(options).CreateTableScript(tableName, checkIfNotExists);
        }

        public static string CreateTableScript<T>(T proto, string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions options = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return CreateTableScript<T>(tableName, checkIfNotExists, options, fromTypeOption);
        }

    }
}
