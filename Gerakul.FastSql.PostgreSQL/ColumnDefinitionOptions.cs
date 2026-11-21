namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// How the columns are declared when a bulk copy creates the table (see <c>CreateTable</c> of the bulk copy
/// options). The class has the shape of the SQL Server one.
/// </summary>
public class ColumnDefinitionOptions
{
    /// <summary>
    /// The names of the columns of the primary key; null or empty for a table without one.
    /// </summary>
    public List<string>? PrimaryKey { get; set; }

    /// <summary>
    /// The length of the columns created for strings: <c>varchar(n)</c>, or <c>text</c> with -1.
    /// </summary>
    public short StringMaxLength { get; set; }

    /// <summary>
    /// No effect on PostgreSQL: byte arrays are created as <c>bytea</c>, which has no length.
    /// </summary>
    public short BytesMaxLength { get; set; }

    /// <summary>
    /// The precision of the columns created for decimals: <c>numeric(precision, scale)</c>.
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// The scale of the columns created for decimals.
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// The number of fractional digits of the seconds in the columns created for <c>DateTime</c>
    /// (<c>timestamp</c>), <c>DateTimeOffset</c> (<c>timestamptz</c>), <c>TimeOnly</c> (<c>time</c>) and
    /// <c>TimeSpan</c> (<c>interval</c>). PostgreSQL allows at most 6; a larger value is taken as 6.
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
    /// <c>varchar(4000)</c>, <c>numeric(38, 18)</c>, 6 fractional digits for dates and times.
    /// </summary>
    public static readonly ColumnDefinitionOptions Default = new ColumnDefinitionOptions()
    {
        StringMaxLength = 4000,
        BytesMaxLength = 8000,
        Precision = 38,
        Scale = 18,
        DateTimeScale = 6
    };

    /// <summary>
    /// <c>text</c>, <c>numeric(38, 18)</c>, 6 fractional digits for dates and times.
    /// </summary>
    public static readonly ColumnDefinitionOptions Max = new ColumnDefinitionOptions()
    {
        StringMaxLength = -1,
        BytesMaxLength = -1,
        Precision = 38,
        Scale = 18,
        DateTimeScale = 6
    };

    /// <summary>
    /// <c>varchar(255)</c>, <c>numeric(19, 9)</c>, 3 fractional digits for dates and times.
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
