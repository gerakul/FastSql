using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

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

    // the reader is closed when the enumeration ends or is abandoned
    private static IEnumerable<T> Query<T>(DbCommand cmd, Func<DbDataReader, IEnumerable<T>> read)
    {
        using DbDataReader reader = cmd.ExecuteReader();
        foreach (var item in read(reader))
        {
            yield return item;
        }
    }

    public static IEnumerable<object[]> ExecuteQuery(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Query(cmd, reader => reader.ReadAll());
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(this DbCommand cmd, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateObjects(r), cancellationToken);
    }

    public static IEnumerable<T> ExecuteQuery<T>(this DbCommand cmd, ReadOptions? readOptions = null)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Query(cmd, reader => reader.ReadAll<T>(readOptions));
    }

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(this DbCommand cmd, ReadOptions? readOptions = null,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.Create<T>(r, readOptions), cancellationToken);
    }

    public static IEnumerable<T> ExecuteQueryAnonymous<T>(this DbCommand cmd, T proto, ReadOptions? readOptions = null)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Query(cmd, reader => reader.ReadAllAnonymous(proto, readOptions));
    }

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(this DbCommand cmd, T proto, ReadOptions? readOptions = null,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, readOptions), cancellationToken);
    }

    public static IEnumerable<object?> ExecuteQueryFirstColumn(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Query(cmd, reader => reader.ReadAllFirstColumn());
    }

    public static IAsyncEnumerable<object?> ExecuteQueryFirstColumnAsync(this DbCommand cmd,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn(r), cancellationToken);
    }

    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(this DbCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return Query(cmd, reader => reader.ReadAllFirstColumn<T>());
    }

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(this DbCommand cmd,
      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return QueryAsync(cmd, r => ReadInfoFactory.CreateFirstColumn<T>(r), cancellationToken);
    }
}
