namespace Gerakul.FastSql.Common;

/// <summary>
/// Options of a bulk copy. A value that is not set (null) is taken from the options of the context, then from the
/// defaults.
/// </summary>
public class BulkOptions
{
    private static readonly BulkOptions defaultOptions = new BulkOptions(true);

    /// <summary>
    /// Which names must have a counterpart: the fields of the source, the columns of the table, both or neither.
    /// Default: <c>Source</c>.
    /// </summary>
    public FieldsSelector? FieldsSelector { get; set; }

    /// <summary>
    /// Whether the fields of the source are matched to the columns of the table with regard to case. Not set by
    /// default.
    /// </summary>
    /// <remarks>
    /// When it is not set and the selector is <c>Source</c>, the names go to the server as they are and the columns
    /// of the table are not read. In the other cases the columns are read, and a value that is not set means no
    /// regard to case.
    /// </remarks>
    public bool? CaseSensitiveFieldsMatching { get; set; }

    /// <summary>
    /// Whether to create the table from the fields of the source before the copy. Default: false. The column types
    /// come from the reader's column schema when the reader belongs to the provider's driver, otherwise from the
    /// field types; the <c>ColumnDefinitionOptions</c> of the provider's options say how they are declared.
    /// </summary>
    public bool? CreateTable { get; set; }

    /// <summary>
    /// When the table is created: whether the column types come from the field types of the reader alone, without
    /// its schema table. Default: false.
    /// </summary>
    public bool? IgnoreDataReaderSchemaTable { get; set; }

    /// <summary>
    /// When the table is created: whether to create it only if it does not exist. Default: false.
    /// </summary>
    public bool? CheckTableIfNotExistsBeforeCreation { get; set; }

    /// <summary>
    /// The timeout of the copy in seconds; 0 is no limit. Not set by default: the default of the driver applies
    /// (<c>SqlBulkCopy</c>: 30 seconds; PostgreSQL: the command timeout of the connection).
    /// </summary>
    /// <remarks>
    /// When it expires, SQL Server throws <c>SqlException</c>; Npgsql throws <c>NpgsqlException</c> with an inner
    /// <c>TimeoutException</c>, and closes the connection.
    /// </remarks>
    public int? BulkCopyTimeout { get; set; }

    /// <summary>
    /// The default values of this type of options.
    /// </summary>
    protected virtual BulkOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public BulkOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null,
      bool? createTable = null, bool? ignoreDataReaderSchemaTable = null,
      bool? checkTableIfNotExistsBeforeCreation = null, int? bulkCopyTimeout = null)
    {
        this.FieldsSelector = fieldsSelector;
        this.CaseSensitiveFieldsMatching = caseSensitiveFieldsMatching;
        this.CreateTable = createTable;
        this.IgnoreDataReaderSchemaTable = ignoreDataReaderSchemaTable;
        this.CheckTableIfNotExistsBeforeCreation = checkTableIfNotExistsBeforeCreation;
        this.BulkCopyTimeout = bulkCopyTimeout;
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected BulkOptions(bool defaultOptions)
    {
        if (defaultOptions)
        {
            this.FieldsSelector = Common.FieldsSelector.Source;
            this.CaseSensitiveFieldsMatching = null;
            this.CreateTable = false;
            this.IgnoreDataReaderSchemaTable = false;
            this.CheckTableIfNotExistsBeforeCreation = false;
            this.BulkCopyTimeout = null;
        }
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected BulkOptions(BulkOptions bulkOptions)
    {
        this.FieldsSelector = bulkOptions.FieldsSelector;
        this.CaseSensitiveFieldsMatching = bulkOptions.CaseSensitiveFieldsMatching;
        this.CreateTable = bulkOptions.CreateTable;
        this.IgnoreDataReaderSchemaTable = bulkOptions.IgnoreDataReaderSchemaTable;
        this.CheckTableIfNotExistsBeforeCreation = bulkOptions.CheckTableIfNotExistsBeforeCreation;
        this.BulkCopyTimeout = bulkOptions.BulkCopyTimeout;
    }

    /// <summary>
    /// Sets every value that is not set from the given options. Returns this object.
    /// </summary>
    protected internal virtual BulkOptions SetDefaults(BulkOptions defaultOptions)
    {
        FieldsSelector ??= defaultOptions.FieldsSelector;
        CaseSensitiveFieldsMatching ??= defaultOptions.CaseSensitiveFieldsMatching;
        CreateTable ??= defaultOptions.CreateTable;
        IgnoreDataReaderSchemaTable ??= defaultOptions.IgnoreDataReaderSchemaTable;
        CheckTableIfNotExistsBeforeCreation ??= defaultOptions.CheckTableIfNotExistsBeforeCreation;
        BulkCopyTimeout ??= defaultOptions.BulkCopyTimeout;

        return this;
    }

    internal BulkOptions SetDefaults()
    {
        return SetDefaults(Default);
    }

    /// <summary>
    /// Creates a copy of the options.
    /// </summary>
    public virtual BulkOptions Clone()
    {
        return new BulkOptions(this);
    }
}
