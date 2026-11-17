namespace Gerakul.FastSql.Common;

/// <summary>
/// Presents a sequence of objects as a data reader, and writes it to a table.
/// </summary>
public static partial class EnumerableExtensions
{
    #region ToDataReader

    private const bool defaultReaderCaseSensitive = true;

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per given setting.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. A field is found by its name with case
    /// (<c>GetOrdinal("id")</c> does not find a field <c>ID</c>).
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, params FieldSettings<T>[] settings)
    {
        return new UniversalDataReader<T>(values, settings, defaultReaderCaseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per given setting.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. A field is found by its name with case
    /// (<c>GetOrdinal("id")</c> does not find a field <c>ID</c>).
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, params FieldSettings<T>[] settings)
    {
        return new UniversalDataReader<T>(values, settings, defaultReaderCaseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per given setting.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, settings, caseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per given setting.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, settings, caseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per public field and property of
    /// <c>T</c> that <c>fromTypeOption</c> selects.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, FromTypeOption fromTypeOption, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, TypeMembers<T>.ForWriting(fromTypeOption), caseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per public field and property of
    /// <c>T</c> that <c>fromTypeOption</c> selects.
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, FromTypeOption fromTypeOption, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, TypeMembers<T>.ForWriting(fromTypeOption), caseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per public field and property of
    /// <c>T</c>, except the ones of collection types (<c>FromTypeOption.Default</c>).
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, TypeMembers<T>.ForWriting(FromTypeOption.Default), caseSensitive);
    }

    /// <summary>
    /// Presents the sequence as a data reader: every object is a row, with a field per public field and property of
    /// <c>T</c>, except the ones of collection types (<c>FromTypeOption.Default</c>).
    /// </summary>
    /// <remarks>
    /// The sequence is enumerated as the reader is read. <c>caseSensitive</c> says whether a field is found by its
    /// name with case (<c>GetOrdinal</c>); by default it is.
    /// </remarks>
    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, bool caseSensitive = defaultReaderCaseSensitive)
    {
        return new UniversalDataReader<T>(values, TypeMembers<T>.ForWriting(FromTypeOption.Default), caseSensitive);
    }

    #endregion

    #region WriteToServer

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static void WriteToServer<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        using var reader = values.ToDataReader();
        reader.WriteToServer(context, destinationTable, bulkOptions, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return WriteToServerAsync(values.ToDataReader(), context, destinationTable, bulkOptions, cancellationToken, fields);
    }

    // the reader belongs to this method, so it is disposed here
    private static async Task WriteToServerAsync<T>(UniversalDataReader<T> reader, DbContext context, string destinationTable, BulkOptions? bulkOptions,
        CancellationToken cancellationToken, string[] fields)
    {
        await using var readerScope = reader.ConfigureAwait(false);
        await DataReaderExtensions.WriteToServerAsync(reader, context, destinationTable, bulkOptions, cancellationToken, fields).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, bulkOptions, CancellationToken.None, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static void WriteToServer<T>(this IEnumerable<T> values, DbContext context, string destinationTable, params string[] fields)
    {
        values.WriteToServer(context, destinationTable, null, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, null, cancellationToken, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, null, CancellationToken.None, fields);
    }

    // The same for a sequence that is produced asynchronously, a FastSql query for example: its rows are taken
    // without blocking a thread. The token is not passed to the sequence; a sequence that needs one gets it where it is created
    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IAsyncEnumerable<T> values, DbContext context, string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinationTable);

        return WriteToServerAsync(values.ToDataReader(), context, destinationTable, bulkOptions, cancellationToken, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IAsyncEnumerable<T> values, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, bulkOptions, CancellationToken.None, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IAsyncEnumerable<T> values, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, null, cancellationToken, fields);
    }

    /// <summary>
    /// Writes the sequence to the table, with the bulk copy of the provider of the context: a column per public
    /// field and property of <c>T</c>.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the members to write; without it all of them are written.
    /// </remarks>
    public static Task WriteToServerAsync<T>(this IAsyncEnumerable<T> values, DbContext context, string destinationTable, params string[] fields)
    {
        return values.WriteToServerAsync(context, destinationTable, null, CancellationToken.None, fields);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// The field settings of the type of the elements, which is how they are got for an anonymous type. The
    /// sequence is not enumerated.
    /// </summary>
    public static FieldSettings<T>[] GetFieldSettings<T>(this IEnumerable<T> values, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FieldSettings.FromType<T>(fromTypeOption);
    }

    #endregion
}
