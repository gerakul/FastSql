namespace Gerakul.FastSql.Common;

/// <summary>
/// Options of reading rows into objects. A value that is not set (null) is taken from the options of the context,
/// then from the defaults.
/// </summary>
public class ReadOptions
{
    internal static readonly ReadOptions defaultOptions = new ReadOptions(true);

    /// <summary>
    /// Which names must have a counterpart: the fields of the reader, the members of the type, both or neither.
    /// Default: <c>Destination</c>, every member must have a field.
    /// </summary>
    public FieldsSelector? FieldsSelector { get; set; }

    /// <summary>
    /// Which members of the type are filled. Default: <c>Default</c>.
    /// </summary>
    public FromTypeOption? FromTypeOption { get; set; }

    /// <summary>
    /// Whether fields are matched to members with regard to case. Default: false.
    /// </summary>
    public bool? CaseSensitiveFieldsMatching { get; set; }

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public ReadOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null, FromTypeOption? fromTypeOption = null)
    {
        this.FieldsSelector = fieldsSelector;
        this.FromTypeOption = fromTypeOption;
        this.CaseSensitiveFieldsMatching = caseSensitiveFieldsMatching;
    }

    internal ReadOptions(bool defaultOptions)
    {
        if (defaultOptions)
        {
            this.FieldsSelector = Common.FieldsSelector.Destination;
            this.FromTypeOption = Common.FromTypeOption.Default;
            this.CaseSensitiveFieldsMatching = false;
        }
    }

    private ReadOptions(ReadOptions readOptions)
    {
        this.FieldsSelector = readOptions.FieldsSelector;
        this.FromTypeOption = readOptions.FromTypeOption;
        this.CaseSensitiveFieldsMatching = readOptions.CaseSensitiveFieldsMatching;
    }

    internal ReadOptions SetDefaults(ReadOptions defaultOptions)
    {
        FieldsSelector ??= defaultOptions.FieldsSelector;
        FromTypeOption ??= defaultOptions.FromTypeOption;
        CaseSensitiveFieldsMatching ??= defaultOptions.CaseSensitiveFieldsMatching;

        return this;
    }

    internal ReadOptions SetDefaults()
    {
        return SetDefaults(defaultOptions);
    }

    /// <summary>
    /// Creates a copy of the options.
    /// </summary>
    public ReadOptions Clone()
    {
        return new ReadOptions(this);
    }
}
