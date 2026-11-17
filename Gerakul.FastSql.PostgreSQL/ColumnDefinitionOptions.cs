namespace Gerakul.FastSql.PostgreSQL;

// Not used: table creation is not implemented for PostgreSQL (bulk copy with CreateTable throws). The class is
// public, so it stays until that is decided
/// <summary>
/// Not used yet: a bulk copy to PostgreSQL does not create the table, and <c>NpgsqlBulkOptions</c> takes no column
/// definition options. The class has the shape of the SQL Server one and stays in the public API until PostgreSQL
/// table creation is decided.
/// </summary>
public class ColumnDefinitionOptions
{
    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public List<string>? PrimaryKey { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public short StringMaxLength { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public short BytesMaxLength { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
    /// </summary>
    public byte DateTimeScale { get; set; }

    /// <summary>
    /// Not used: a bulk copy to PostgreSQL does not create the table.
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
    /// The values of the SQL Server options of this name. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
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
    /// The values of the SQL Server options of this name. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
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
    /// The values of the SQL Server options of this name. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
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
    /// A copy of the options with the given primary key. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
    /// </summary>
    public static ColumnDefinitionOptions FromOption(ColumnDefinitionOptions option, params string[] primaryKey)
    {
        var o = option.Clone();
        o.PrimaryKey = primaryKey.ToList();
        return o;
    }

    /// <summary>
    /// A copy of <c>Default</c> with the given primary key. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
    /// </summary>
    public static ColumnDefinitionOptions FromDefault(params string[] primaryKey)
    {
        return FromOption(Default, primaryKey);
    }

    /// <summary>
    /// A copy of <c>Max</c> with the given primary key. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
    /// </summary>
    public static ColumnDefinitionOptions FromMax(params string[] primaryKey)
    {
        return FromOption(Max, primaryKey);
    }

    /// <summary>
    /// A copy of <c>Small</c> with the given primary key. Not used: a bulk copy to PostgreSQL does not create the
    /// table.
    /// </summary>
    public static ColumnDefinitionOptions FromSmall(params string[] primaryKey)
    {
        return FromOption(Small, primaryKey);
    }
}
