using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// Options of a bulk copy to PostgreSQL.
/// </summary>
public class NpgsqlBulkOptions : BulkOptions
{
    private static readonly NpgsqlBulkOptions defaultOptions = new NpgsqlBulkOptions(true);

    /// <summary>
    /// How the columns are declared when the table is created.
    /// </summary>
    public ColumnDefinitionOptions? ColumnDefinitionOptions { get; set; }

    /// <inheritdoc />
    protected override BulkOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public NpgsqlBulkOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null,
      bool? createTable = null, ColumnDefinitionOptions? columnDefinitionOptions = null, bool? ignoreDataReaderSchemaTable = null,
      bool? checkTableIfNotExistsBeforeCreation = null, int? bulkCopyTimeout = null)
        : base(fieldsSelector, caseSensitiveFieldsMatching, createTable, ignoreDataReaderSchemaTable, checkTableIfNotExistsBeforeCreation,
              bulkCopyTimeout)
    {
        this.ColumnDefinitionOptions = columnDefinitionOptions;
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected NpgsqlBulkOptions(bool defaultOptions)
        : base(defaultOptions)
    {
        if (defaultOptions)
        {
            this.ColumnDefinitionOptions = ColumnDefinitionOptions.Default;
        }
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected internal NpgsqlBulkOptions(BulkOptions bulkOptions)
        : base(bulkOptions)
    {
        if (bulkOptions is NpgsqlBulkOptions opt)
        {
            this.ColumnDefinitionOptions = opt.ColumnDefinitionOptions;
        }
    }

    /// <inheritdoc />
    protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
    {
        base.SetDefaults(defaultOptions);
        if (defaultOptions is not NpgsqlBulkOptions opt)
        {
            return this;
        }

        ColumnDefinitionOptions ??= opt.ColumnDefinitionOptions?.Clone();

        return this;
    }

    /// <inheritdoc />
    public override BulkOptions Clone()
    {
        return new NpgsqlBulkOptions(this);
    }
}
