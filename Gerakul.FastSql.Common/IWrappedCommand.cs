using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A command that is ready to run. On a connection string context each run opens a connection and closes it when
/// the result is read to the end or disposed; on the other contexts it runs on their connection.
/// </summary>
public interface IWrappedCommand
{
    /// <summary>
    /// Runs the command. Returns the number of rows affected.
    /// </summary>
    int ExecuteNonQuery();

    /// <summary>
    /// Runs the command. Returns the number of rows affected.
    /// </summary>
    Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the command and returns the first field of the first row as it is: <c>DBNull</c> for a null,
    /// <c>null</c> when there is no row.
    /// </summary>
    object? ExecuteScalar();

    /// <summary>
    /// Runs the command and returns the first field of the first row as it is: <c>DBNull</c> for a null,
    /// <c>null</c> when there is no row.
    /// </summary>
    Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into arrays of values, one per row.
    /// </summary>
    IEnumerable<object[]> ExecuteQuery();

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into arrays of values, one per row.
    /// </summary>
    IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of <c>T</c>, one row at a
    /// time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    IEnumerable<T> ExecuteQuery<T>(ReadOptions? readOptions = null);

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of <c>T</c>, one row at a
    /// time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions? readOptions = null);

    /// <summary>
    /// When the result is enumerated, runs the command and reads its rows into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row as it is, with a
    /// database null as <c>null</c>.
    /// </summary>
    IEnumerable<object?> ExecuteQueryFirstColumn();

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row as it is, with a
    /// database null as <c>null</c>.
    /// </summary>
    IAsyncEnumerable<object?> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row, converted to
    /// <c>T</c> the way members are mapped.
    /// </summary>
    IEnumerable<T> ExecuteQueryFirstColumn<T>();

    /// <summary>
    /// When the result is enumerated, runs the command and reads the first field of every row, converted to
    /// <c>T</c> the way members are mapped.
    /// </summary>
    IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the command and gives its reader to the delegate. The reader is closed when the delegate returns, and
    /// so is the connection of a connection string context.
    /// </summary>
    void UseReader(Action<DbDataReader> action);

    /// <summary>
    /// Runs the command and gives its reader to the delegate. The reader is closed when the delegate is done, and
    /// so is the connection of a connection string context.
    /// </summary>
    Task UseReaderAsync(CancellationToken cancellationToken, Func<DbDataReader, Task> action);
}

/// <summary>
/// Additions to <c>IWrappedCommand</c>: a typed scalar, and writing the result of a command to a table.
/// </summary>
public static class WrappedCommandExtensions
{
    /// <summary>
    /// Runs the command and gives its reader to the delegate. The reader is closed when the delegate is done, and
    /// so is the connection of a connection string context.
    /// </summary>
    public static Task UseReaderAsync(this IWrappedCommand cmd, Func<DbDataReader, Task> action)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return cmd.UseReaderAsync(CancellationToken.None, action);
    }

    #region ExecuteScalar

    // The first column of the first row as T, converted as the values of a query are (enums, nullable types, dates
    // and times, the invariant culture). Without a row, or when the value is null in the database, the result is
    // null for a T that has null (a class, int?), and an InvalidCastException for any other T
    /// <summary>
    /// Runs the command and returns the first field of the first row, converted to <c>T</c> the way rows are
    /// mapped. A null, or no row, gives <c>null</c> for a <c>T</c> that can be null and an
    /// <c>InvalidCastException</c> otherwise.
    /// </summary>
    public static T ExecuteScalar<T>(this IWrappedCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return ValueConverter.FromScalar<T>(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Runs the command and returns the first field of the first row, converted to <c>T</c> the way rows are
    /// mapped. A null, or no row, gives <c>null</c> for a <c>T</c> that can be null and an
    /// <c>InvalidCastException</c> otherwise.
    /// </summary>
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

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        cmd.UseReader(x => x.WriteToServer(context, destinationTable, bulkOptions, fields));
    }

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(cancellationToken, x => x.WriteToServerAsync(context, destinationTable, bulkOptions, cancellationToken, fields));
    }

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, bulkOptions, fields));
    }

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static void WriteToServer(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        cmd.UseReader(x => x.WriteToServer(context, destinationTable, fields));
    }

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(cancellationToken, x => x.WriteToServerAsync(context, destinationTable, cancellationToken, fields));
    }

    /// <summary>
    /// Runs the command and writes its rows to the table of <c>context</c>, with the bulk copy of its provider. The
    /// context may be of another database: this is how data is copied between databases.
    /// </summary>
    public static Task WriteToServerAsync(this IWrappedCommand cmd, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return cmd.UseReaderAsync(x => x.WriteToServerAsync(context, destinationTable, fields));
    }

    #endregion
}
