using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

/// <summary>
/// Runs a <c>DbCommand</c> and reads its rows into objects. These methods are for a command of your own; the
/// commands of FastSql run through <c>IWrappedCommand</c>.
/// </summary>
public static class DbCommandExtensions
{
    // a cancelled enumeration throws, so partial results can't pass for complete ones; the enumeration's token is
    // checked first, a cancelled query token surfaces as the driver's exception
    private static async IAsyncEnumerable<T> QueryAsync<T>(DbCommand cmd, Func<IDataReader, ReadInfo<T>> readInfoGetter,
      CancellationToken queryToken, [EnumeratorCancellation] CancellationToken enumerationToken = default)
    {
        enumerationToken.ThrowIfCancellationRequested();
        using var linkedTokenSource = Helpers.LinkTokens(queryToken, enumerationToken, out var cancellationToken);

        var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var info = readInfoGetter(reader);

        while (true)
        {
            // drivers may skip the token when the next row is already buffered
            cancellationToken.ThrowIfCancellationRequested();
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }

            yield return info.GetValue();
        }
    }

    // The sync queries check their arguments and return an iterator of their own, which closes the reader when the
    // enumeration ends or is abandoned. One shared iterator that takes the reading as a delegate made every row of
    // a query about a quarter slower
    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into arrays of values, one per row.
    /// </summary>
    public static IEnumerable<object[]> ExecuteQuery(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Rows(cmd);

        static IEnumerable<object[]> Rows(DbCommand cmd)
        {
            using DbDataReader reader = cmd.ExecuteReader();
            foreach (var item in reader.ReadAll())
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into arrays of values, one per row.
    /// </summary>
    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(this DbCommand cmd, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateObjects(r), cancellationToken);
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of <c>T</c>, one row at a
    /// time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IEnumerable<T> ExecuteQuery<T>(this DbCommand cmd, ReadOptions? readOptions = null)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Rows(cmd, readOptions);

        static IEnumerable<T> Rows(DbCommand cmd, ReadOptions? readOptions)
        {
            using DbDataReader reader = cmd.ExecuteReader();
            foreach (var item in reader.ReadAll<T>(readOptions))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of <c>T</c>, one row at a
    /// time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(this DbCommand cmd, ReadOptions? readOptions = null,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.Create<T>(r, readOptions), cancellationToken);
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IEnumerable<T> ExecuteQueryAnonymous<T>(this DbCommand cmd, T proto, ReadOptions? readOptions = null)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Rows(cmd, proto, readOptions);

        static IEnumerable<T> Rows(DbCommand cmd, T proto, ReadOptions? readOptions)
        {
            using DbDataReader reader = cmd.ExecuteReader();
            foreach (var item in reader.ReadAllAnonymous(proto, readOptions))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(this DbCommand cmd, T proto, ReadOptions? readOptions = null,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, readOptions), cancellationToken);
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row as it is, with a
    /// database null as <c>null</c>.
    /// </summary>
    public static IEnumerable<object?> ExecuteQueryFirstColumn(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Rows(cmd);

        static IEnumerable<object?> Rows(DbCommand cmd)
        {
            using DbDataReader reader = cmd.ExecuteReader();
            foreach (var item in reader.ReadAllFirstColumn())
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row as it is, with a
    /// database null as <c>null</c>.
    /// </summary>
    public static IAsyncEnumerable<object?> ExecuteQueryFirstColumnAsync(this DbCommand cmd,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn(r), cancellationToken);
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row, converted to
    /// <c>T</c> the way members are mapped.
    /// </summary>
    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Rows(cmd);

        static IEnumerable<T> Rows(DbCommand cmd)
        {
            using DbDataReader reader = cmd.ExecuteReader();
            foreach (var item in reader.ReadAllFirstColumn<T>())
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row, converted to
    /// <c>T</c> the way members are mapped.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(this DbCommand cmd,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn<T>(r), cancellationToken);
    }
}
