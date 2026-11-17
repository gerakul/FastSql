using Gerakul.FastSql.Common;
using System.Diagnostics.CodeAnalysis;

namespace Gerakul.FastSql.SqlServer;

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
        [typeof(DateTimeOffset)] = "datetimeoffset",
        [typeof(DateOnly)] = "date",
        [typeof(TimeOnly)] = "time",
        [typeof(TimeSpan)] = "time",
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
            "datetime2" or "datetimeoffset" or "time" => new ColumnDefinition(name, typeName, isPrimaryKey, 0, 0, opt.DateTimeScale, isNullable),
            "nvarchar" => new ColumnDefinition(name, typeName, isPrimaryKey, opt.StringMaxLength, 0, 0, isNullable),
            "varbinary" => new ColumnDefinition(name, typeName, isPrimaryKey, opt.BytesMaxLength, 0, 0, isNullable),
            _ => new ColumnDefinition(name, typeName, isPrimaryKey, 0, 0, 0, isNullable)
        };
    }

}

/// <summary>
/// How the columns are declared when a bulk copy creates the table (see <c>CreateTable</c> of the bulk copy
/// options).
/// </summary>
public class ColumnDefinitionOptions
{
    /// <summary>
    /// The names of the columns of the primary key; null or empty for a table without one.
    /// </summary>
    public List<string>? PrimaryKey { get; set; }

    /// <summary>
    /// The length of the columns created for strings: <c>nvarchar(n)</c>, or <c>nvarchar(max)</c> with -1.
    /// </summary>
    public short StringMaxLength { get; set; }

    /// <summary>
    /// The length of the columns created for byte arrays: <c>varbinary(n)</c>, or <c>varbinary(max)</c> with -1.
    /// </summary>
    public short BytesMaxLength { get; set; }

    /// <summary>
    /// The precision of the columns created for decimals.
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// The scale of the columns created for decimals.
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// The scale of the columns created for <c>DateTime</c>, <c>DateTimeOffset</c>, <c>TimeOnly</c> and
    /// <c>TimeSpan</c>.
    /// </summary>
    public byte DateTimeScale { get; set; }

    /// <summary>
    /// Whether a field whose type has no column type throws <c>ArgumentOutOfRangeException</c> as soon as it is
    /// met. Otherwise the table is not created either, and the exception names all such fields.
    /// </summary>
    public bool ThrowIfUnsupportedType { get; set; } = false;

    /// <summary>
    /// Creates a copy of the options.
    /// </summary>
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

    /// <summary>
    /// <c>nvarchar(4000)</c>, <c>varbinary(8000)</c>, <c>decimal(38, 18)</c>, a date and time scale of 7.
    /// </summary>
    public static readonly ColumnDefinitionOptions Default = new ColumnDefinitionOptions()
    {
        StringMaxLength = 4000,
        BytesMaxLength = 8000,
        Precision = 38,
        Scale = 18,
        DateTimeScale = 7
    };

    /// <summary>
    /// <c>nvarchar(max)</c>, <c>varbinary(max)</c>, <c>decimal(38, 18)</c>, a date and time scale of 7.
    /// </summary>
    public static readonly ColumnDefinitionOptions Max = new ColumnDefinitionOptions()
    {
        StringMaxLength = -1,
        BytesMaxLength = -1,
        Precision = 38,
        Scale = 18,
        DateTimeScale = 7
    };

    /// <summary>
    /// <c>nvarchar(255)</c>, <c>varbinary(255)</c>, <c>decimal(19, 9)</c>, a date and time scale of 3.
    /// </summary>
    public static readonly ColumnDefinitionOptions Small = new ColumnDefinitionOptions()
    {
        StringMaxLength = 255,
        BytesMaxLength = 255,
        Precision = 19,
        Scale = 9,
        DateTimeScale = 3
    };

    /// <summary>
    /// A copy of the options with the given primary key.
    /// </summary>
    public static ColumnDefinitionOptions FromOption(ColumnDefinitionOptions option, params string[] primaryKey)
    {
        var o = option.Clone();
        o.PrimaryKey = primaryKey.ToList();
        return o;
    }

    /// <summary>
    /// A copy of <c>Default</c> with the given primary key.
    /// </summary>
    public static ColumnDefinitionOptions FromDefault(params string[] primaryKey)
    {
        return FromOption(Default, primaryKey);
    }

    /// <summary>
    /// A copy of <c>Max</c> with the given primary key.
    /// </summary>
    public static ColumnDefinitionOptions FromMax(params string[] primaryKey)
    {
        return FromOption(Max, primaryKey);
    }

    /// <summary>
    /// A copy of <c>Small</c> with the given primary key.
    /// </summary>
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
