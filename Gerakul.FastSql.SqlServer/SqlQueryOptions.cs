using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// Options of a SQL Server command.
/// </summary>
public class SqlQueryOptions : QueryOptions
{
    private static readonly SqlQueryOptions defaultOptions = new SqlQueryOptions(true);

    /// <inheritdoc />
    protected override QueryOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public SqlQueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
        : base(commandTimeoutSeconds, caseSensitiveParamsMatching)
    {
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected SqlQueryOptions(bool defaultOptions)
        : base(defaultOptions)
    {
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected internal SqlQueryOptions(QueryOptions queryOptions)
        : base(queryOptions)
    {
    }

    /// <inheritdoc />
    protected override QueryOptions SetDefaults(QueryOptions defaultOptions)
    {
        base.SetDefaults(defaultOptions);
        return this;
    }

    /// <inheritdoc />
    public override QueryOptions Clone()
    {
        return new SqlQueryOptions(this);
    }
}
