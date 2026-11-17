using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// Options of a bulk copy to PostgreSQL.
/// </summary>
public class NpgsqlBulkOptions : BulkOptions
{
    private static readonly NpgsqlBulkOptions defaultOptions = new NpgsqlBulkOptions(true);

    /// <inheritdoc />
    protected override BulkOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public NpgsqlBulkOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null)
        : base(fieldsSelector, caseSensitiveFieldsMatching, null, null, null)
    {
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected NpgsqlBulkOptions(bool defaultOptions)
        : base(defaultOptions)
    {
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected internal NpgsqlBulkOptions(BulkOptions bulkOptions)
        : base(bulkOptions)
    {
    }

    /// <inheritdoc />
    protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
    {
        base.SetDefaults(defaultOptions);
        return this;
    }

    /// <inheritdoc />
    public override BulkOptions Clone()
    {
        return new NpgsqlBulkOptions(this);
    }
}
