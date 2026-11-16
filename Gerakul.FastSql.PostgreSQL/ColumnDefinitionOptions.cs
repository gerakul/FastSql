namespace Gerakul.FastSql.PostgreSQL;

// Not used: table creation is not implemented for PostgreSQL (bulk copy with CreateTable throws). The class is
// public, so it stays until that is decided
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
