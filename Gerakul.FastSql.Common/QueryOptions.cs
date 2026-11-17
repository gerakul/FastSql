namespace Gerakul.FastSql.Common;

/// <summary>
/// Options of a command. A value that is not set (null) is taken from the options of the context, then from the
/// defaults.
/// </summary>
public class QueryOptions
{
    private static readonly QueryOptions defaultOptions = new QueryOptions(true);

    /// <summary>
    /// The timeout of the command in seconds. Not set by default: the timeout of the driver applies.
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }

    /// <summary>
    /// Whether the parameters of a command are matched to the fields of the value with regard to case. Default:
    /// false.
    /// </summary>
    public bool? CaseSensitiveParamsMatching { get; set; }

    /// <summary>
    /// The default values of this type of options.
    /// </summary>
    protected virtual QueryOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public QueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
    {
        this.CommandTimeoutSeconds = commandTimeoutSeconds;
        this.CaseSensitiveParamsMatching = caseSensitiveParamsMatching;
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected QueryOptions(bool defaultOptions)
    {
        if (defaultOptions)
        {
            this.CommandTimeoutSeconds = null;
            this.CaseSensitiveParamsMatching = false;
        }
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected QueryOptions(QueryOptions queryOptions)
    {
        this.CommandTimeoutSeconds = queryOptions.CommandTimeoutSeconds;
        this.CaseSensitiveParamsMatching = queryOptions.CaseSensitiveParamsMatching;
    }

    /// <summary>
    /// Sets every value that is not set from the given options. Returns this object.
    /// </summary>
    protected internal virtual QueryOptions SetDefaults(QueryOptions defaultOptions)
    {
        CommandTimeoutSeconds ??= defaultOptions.CommandTimeoutSeconds;
        CaseSensitiveParamsMatching ??= defaultOptions.CaseSensitiveParamsMatching;

        return this;
    }

    /// <summary>
    /// Sets every value that is not set from the defaults of this type of options. Returns this object.
    /// </summary>
    protected internal virtual QueryOptions SetDefaults()
    {
        return SetDefaults(Default);
    }

    /// <summary>
    /// Creates a copy of the options.
    /// </summary>
    public virtual QueryOptions Clone()
    {
        return new QueryOptions(this);
    }
}
