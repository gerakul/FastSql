using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// Options of a bulk copy to SQL Server.
/// </summary>
public class SqlBulkOptions : BulkOptions
{
    private static readonly SqlBulkOptions defaultOptions = new SqlBulkOptions(true);

    /// <summary>
    /// The number of rows in a batch, as <c>BatchSize</c> of <c>SqlBulkCopy</c>. Not set by default.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// The timeout in seconds, as <c>BulkCopyTimeout</c> of <c>SqlBulkCopy</c>. Not set by default.
    /// </summary>
    public int? BulkCopyTimeout { get; set; }

    /// <summary>
    /// The options given to <c>SqlBulkCopy</c>. Default: <c>SqlBulkCopyOptions.Default</c>.
    /// </summary>
    public SqlBulkCopyOptions? SqlBulkCopyOptions { get; set; }

    /// <summary>
    /// As <c>EnableStreaming</c> of <c>SqlBulkCopy</c>. Not set by default.
    /// </summary>
    public bool? EnableStreaming { get; set; }

    /// <summary>
    /// How the columns are declared when the table is created.
    /// </summary>
    public ColumnDefinitionOptions? ColumnDefinitionOptions { get; set; }

    /// <inheritdoc />
    protected override BulkOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public SqlBulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions? sqlBulkCopyOptions = null,
      FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null, bool? enableStreaming = null,
      bool? createTable = null, ColumnDefinitionOptions? columnDefinitionOptions = null, bool? ignoreDataReaderSchemaTable = null,
      bool? checkTableIfNotExistsBeforeCreation = null)
        : base(fieldsSelector, caseSensitiveFieldsMatching, createTable, ignoreDataReaderSchemaTable, checkTableIfNotExistsBeforeCreation)
    {
        this.BatchSize = batchSize;
        this.BulkCopyTimeout = bulkCopyTimeout;
        this.SqlBulkCopyOptions = sqlBulkCopyOptions;
        this.EnableStreaming = enableStreaming;
        this.ColumnDefinitionOptions = columnDefinitionOptions;
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected SqlBulkOptions(bool defaultOptions)
        : base(defaultOptions)
    {
        if (defaultOptions)
        {
            this.BatchSize = null;
            this.BulkCopyTimeout = null;
            this.SqlBulkCopyOptions = Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default;
            this.EnableStreaming = null;
            this.ColumnDefinitionOptions = ColumnDefinitionOptions.Default;
        }
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected internal SqlBulkOptions(BulkOptions bulkOptions)
        : base(bulkOptions)
    {
        if (bulkOptions is SqlBulkOptions opt)
        {
            this.BatchSize = opt.BatchSize;
            this.BulkCopyTimeout = opt.BulkCopyTimeout;
            this.SqlBulkCopyOptions = opt.SqlBulkCopyOptions;
            this.EnableStreaming = opt.EnableStreaming;
            this.ColumnDefinitionOptions = opt.ColumnDefinitionOptions;
        }
        else
        {
            this.BatchSize = null;
            this.BulkCopyTimeout = null;
            this.SqlBulkCopyOptions = null;
            this.EnableStreaming = null;
            this.ColumnDefinitionOptions = null;
        }
    }

    /// <inheritdoc />
    protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
    {
        base.SetDefaults(defaultOptions);
        if (defaultOptions is not SqlBulkOptions opt)
        {
            return this;
        }

        BatchSize ??= opt.BatchSize;
        BulkCopyTimeout ??= opt.BulkCopyTimeout;
        SqlBulkCopyOptions ??= opt.SqlBulkCopyOptions;
        EnableStreaming ??= opt.EnableStreaming;
        ColumnDefinitionOptions ??= opt.ColumnDefinitionOptions?.Clone();

        return this;
    }

    /// <inheritdoc />
    public override BulkOptions Clone()
    {
        return new SqlBulkOptions(this);
    }
}
