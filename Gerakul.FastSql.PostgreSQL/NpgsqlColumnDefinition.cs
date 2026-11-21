using Gerakul.FastSql.Common;
using Npgsql.Schema;
using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.PostgreSQL;

// A column of a table that a bulk copy creates
internal sealed class NpgsqlColumnDefinition
{
    // the largest number of fractional digits of timestamp, time and interval
    private const byte MaxDateTimeScale = 6;

    public string Name { get; }
    public string TypeDeclaration { get; }
    public bool IsPrimaryKey { get; }
    public bool IsNullable { get; }

    private NpgsqlColumnDefinition(string name, string typeDeclaration, bool isPrimaryKey, bool isNullable)
    {
        this.Name = name;
        this.TypeDeclaration = typeDeclaration;
        this.IsPrimaryKey = isPrimaryKey;
        this.IsNullable = isNullable;
    }

    // a double quote inside a name is written twice; the name keeps its case, as in the commands of the text generator
    public string GetColumnDeclaration()
    {
        return $"\"{Name.Replace("\"", "\"\"")}\" {TypeDeclaration} {(IsPrimaryKey ? "primary key not null" : (IsNullable ? "null" : "not null"))}";
    }

    // the column types of the supported member types, chosen so that what binary COPY writes for the member type fits
    // the column; a Nullable<T> member gets the column type of T
    private static readonly Dictionary<Type, Func<ColumnDefinitionOptions, string>> columnTypes = new Dictionary<Type, Func<ColumnDefinitionOptions, string>>()
    {
        [typeof(bool)] = _ => "boolean",
        [typeof(byte)] = _ => "smallint",
        [typeof(short)] = _ => "smallint",
        [typeof(int)] = _ => "integer",
        [typeof(long)] = _ => "bigint",
        [typeof(float)] = _ => "real",
        [typeof(double)] = _ => "double precision",
        [typeof(decimal)] = o => $"numeric({o.Precision}, {o.Scale})",
        [typeof(Guid)] = _ => "uuid",
        [typeof(DateTime)] = o => $"timestamp({DateTimeScale(o)})",
        [typeof(DateTimeOffset)] = o => $"timestamptz({DateTimeScale(o)})",
        [typeof(DateOnly)] = _ => "date",
        [typeof(TimeOnly)] = o => $"time({DateTimeScale(o)})",
        [typeof(TimeSpan)] = o => $"interval({DateTimeScale(o)})",
        [typeof(string)] = o => o.StringMaxLength == -1 ? "text" : $"varchar({o.StringMaxLength})",
        [typeof(byte[])] = _ => "bytea"
    };

    private static byte DateTimeScale(ColumnDefinitionOptions options)
    {
        return Math.Min(options.DateTimeScale, MaxDateTimeScale);
    }

    public static NpgsqlColumnDefinition? FromFieldType(Type type, string name, ColumnDefinitionOptions? options = null)
    {
        var opt = options ?? ColumnDefinitionOptions.Default;
        bool isPrimaryKey = opt.PrimaryKey?.Contains(name) ?? false;

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (!columnTypes.TryGetValue(underlyingType ?? type, out var typeDeclaration))
        {
            if (opt.ThrowIfUnsupportedType)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported type");
            }

            return null;
        }

        // reference types (string, byte[]) are nullable, value types only as Nullable<T>
        bool isNullable = !type.IsValueType || underlyingType != null;

        return new NpgsqlColumnDefinition(name, typeDeclaration(opt), isPrimaryKey, isNullable);
    }

    // Npgsql reports a type without its size ("character varying", "numeric"); the size comes from the other values of
    // the schema, and a type without a reported size is declared without one. Other types (arrays, user-defined
    // types) are declared as they are reported
    private static string GetTypeDeclaration(DbColumn column)
    {
        var typeName = column.DataTypeName!;

        return typeName switch
        {
            "character varying" or "character" or "bit" or "bit varying" when column.ColumnSize is > 0 => $"{typeName}({column.ColumnSize})",
            "numeric" when column.NumericPrecision is > 0 => $"numeric({column.NumericPrecision}, {column.NumericScale ?? 0})",
            "timestamp without time zone" or "timestamp with time zone" or "time without time zone" or "time with time zone"
                when column.NumericPrecision.HasValue => typeName.Insert(typeName.IndexOf(' '), $"({column.NumericPrecision})"),
            "interval" when column.NumericPrecision.HasValue => $"interval({column.NumericPrecision})",
            _ => typeName
        };
    }

    // Npgsql reports nullability and keys only for a reader opened with KeyInfo; an unknown nullability is nullable
    private static NpgsqlColumnDefinition FromSchema(DbColumn column, string[] primaryKey)
    {
        return new NpgsqlColumnDefinition(column.ColumnName, GetTypeDeclaration(column),
            (column.IsKey ?? false) || primaryKey.Contains(column.ColumnName), column.AllowDBNull ?? true);
    }

    // The columns from the reader's column schema when Npgsql made it (its columns are NpgsqlDbColumn, which a wrapper
    // around an Npgsql reader passes on); otherwise, or with ignoreSchemaTable, from the field types. Null for a field
    // whose type has no column type
    public static IEnumerable<NpgsqlColumnDefinition?> GetColumnDefinitions(IDataReader reader, ColumnDefinitionOptions? options, bool ignoreSchemaTable)
    {
        if (!ignoreSchemaTable && reader is DbDataReader dbReader && !IsUniversalDataReader(reader))
        {
            var schema = dbReader.GetColumnSchema();
            if (schema.All(x => x is NpgsqlDbColumn))
            {
                var primaryKey = options?.PrimaryKey?.ToArray() ?? [];
                return schema.Select(x => FromSchema(x, primaryKey)).ToArray();
            }
        }

        return Enumerable.Range(0, reader.FieldCount).Select(i => FromFieldType(reader.GetFieldType(i), reader.GetName(i), options)).ToArray();
    }

    // UniversalDataReader has no schema table (GetColumnSchema throws); its field types describe the columns
    private static bool IsUniversalDataReader(IDataReader reader)
    {
        for (var type = reader.GetType(); type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UniversalDataReader<>))
            {
                return true;
            }
        }

        return false;
    }

    // the layout of the SQL Server script, with the line breaks of the platform whatever the line breaks of this file
    public static string CreateTableScript(IEnumerable<NpgsqlColumnDefinition> columnDefinitions, string tableName, bool checkIfNotExists)
    {
        var nl = Environment.NewLine;
        var columns = string.Join(nl + "\t,", columnDefinitions.Select(x => x.GetColumnDeclaration()));

        return $"{nl}create table {(checkIfNotExists ? "if not exists " : "")}{tableName}{nl}({nl}\t {columns}{nl}){nl}";
    }
}
