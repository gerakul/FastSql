using System.Data.Common;

namespace Gerakul.FastSql.Common;

public interface IWrappedCommand
{
    int ExecuteNonQuery();
    Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
    object? ExecuteScalar();
    Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);

    IEnumerable<object[]> ExecuteQuery();
    IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default);
    IEnumerable<T> ExecuteQuery<T>(ReadOptions? readOptions = null);
    IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions? readOptions = null, CancellationToken cancellationToken = default);
    IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions? readOptions = null);
    IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions? readOptions = null, CancellationToken cancellationToken = default);
    IEnumerable<object?> ExecuteQueryFirstColumn();
    IAsyncEnumerable<object?> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default);
    IEnumerable<T> ExecuteQueryFirstColumn<T>();
    IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default);

    void UseReader(Action<DbDataReader> action);
    Task UseReaderAsync(CancellationToken cancellationToken, Func<DbDataReader, Task> action);
}

public static class WrappedCommandExtensions
{
    public static Task UseReaderAsync(this IWrappedCommand cmd, Func<DbDataReader, Task> action)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return cmd.UseReaderAsync(CancellationToken.None, action);
    }

    #region ExecuteScalar

    // The first column of the first row as T, converted as the values of a query are (enums, nullable types, dates
    // and times, the invariant culture). Without a row, or when the value is null in the database, the result is
    // null for a T that has null (a class, int?), and an InvalidCastException for any other T
    public static T ExecuteScalar<T>(this IWrappedCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return ValueConverter.FromScalar<T>(cmd.ExecuteScalar());
    }

    public static Task<T> ExecuteScalarAsync<T>(this IWrappedCommand cmd, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return ExecuteScalarCoreAsync<T>(cmd, cancellationToken);
    }

    private static async Task<T> ExecuteScalarCoreAsync<T>(IWrappedCommand cmd, CancellationToken cancellationToken)
    {
        return ValueConverter.FromScalar<T>(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    #endregion

    #region WriteToServer

    public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        cmd.UseReader(x => x.WriteToServer(context, destinationTable, bulkOptions, fields));
    }

    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(cancellationToken, x => x.WriteToServerAsync(context, destinationTable, bulkOptions, cancellationToken, fields));
    }

    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, bulkOptions, fields));
    }

    public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        cmd.UseReader(x => x.WriteToServer(context, destinationTable, fields));
    }

    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(cancellationToken, x => x.WriteToServerAsync(context, destinationTable, cancellationToken, fields));
    }

    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, fields));
    }

    #endregion
}
