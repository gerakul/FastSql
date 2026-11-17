using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// Options of a PostgreSQL command.
/// </summary>
public class NpgsqlQueryOptions : QueryOptions
{
    private static readonly NpgsqlQueryOptions defaultOptions = new NpgsqlQueryOptions(true);

    /// <inheritdoc />
    protected override QueryOptions Default => defaultOptions;

    /// <summary>
    /// Creates options with the given values; the ones left out are not set.
    /// </summary>
    public NpgsqlQueryOptions(int? commandTimeoutSeconds = null, bool? caseSensitiveParamsMatching = null)
        : base(commandTimeoutSeconds, caseSensitiveParamsMatching)
    {
    }

    /// <summary>
    /// Creates options with the default values (<c>true</c>) or with nothing set (<c>false</c>).
    /// </summary>
    protected NpgsqlQueryOptions(bool defaultOptions)
        : base(defaultOptions)
    {
    }

    /// <summary>
    /// Creates a copy of the given options.
    /// </summary>
    protected internal NpgsqlQueryOptions(QueryOptions queryOptions)
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
        return new NpgsqlQueryOptions(this);
    }

}
