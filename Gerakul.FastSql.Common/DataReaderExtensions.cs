using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

/// <summary>
/// Reads the rows of a data reader into objects, and writes a data reader to a table.
/// </summary>
public static partial class DataReaderExtensions
{
    #region Read

    // the reader belongs to the caller, so it stays open; a cancelled enumeration throws, so partial results can't
    // pass for complete ones
    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(Func<ReadInfo<T>> readInfoGetter,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = readInfoGetter();

        while (true)
        {
            // drivers may skip the token when the next row is already buffered
            cancellationToken.ThrowIfCancellationRequested();
            if (!await info.Reader.ReadAsyncInternal(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }

            yield return info.GetValue();
        }
    }

    private static IEnumerable<T> CreateEnumerable<T>(Func<ReadInfo<T>> readInfoGetter)
    {
        var info = readInfoGetter();
        IDataReader reader = info.Reader;

        while (reader.Read())
        {
            yield return info.GetValue();
        }
    }

    internal static Task<bool> ReadAsyncInternal(this IDataReader reader, CancellationToken cancellationToken)
    {
        if (reader is DbDataReader dbReader)
        {
            return dbReader.ReadAsync(cancellationToken);
        }

        return Task.Run(() => reader.Read(), cancellationToken);
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into arrays of values, one per row.
    /// </summary>
    public static IEnumerable<object[]> ReadAll(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateEnumerable(() => ReadInfoFactory.CreateObjects(reader));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into arrays of values, one per row.
    /// </summary>
    public static IAsyncEnumerable<object[]> ReadAllAsync(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateAsyncEnumerable(() => ReadInfoFactory.CreateObjects(reader));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of <c>T</c>, one row at a time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IEnumerable<T> ReadAll<T>(this IDataReader reader, ReadOptions? readOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateEnumerable(() => ReadInfoFactory.Create<T>(reader, readOptions));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of <c>T</c>, one row at a time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader, ReadOptions? readOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateAsyncEnumerable(() => ReadInfoFactory.Create<T>(reader, readOptions));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of <c>T</c>, one row at a time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IEnumerable<T> ReadAll<T>(this IDataReader reader)
    {
        return ReadAll<T>(reader, null);
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of <c>T</c>, one row at a time.
    /// </summary>
    /// <remarks>
    /// Fields are matched to the public fields and properties of <c>T</c> by name, as the <c>ReadOptions</c> say. A
    /// type without a parameterless constructor, such as a positional record, is created through its constructor
    /// with the most parameters, whose parameters are matched by name as well.
    /// </remarks>
    public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader)
    {
        return ReadAllAsync<T>(reader, null);
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto, ReadOptions? readOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto, ReadOptions? readOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateAsyncEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto)
    {
        return ReadAllAnonymous(reader, proto, null);
    }

    /// <summary>
    /// As the result is enumerated, reads the rows left in the reader into objects of the type of <c>proto</c>,
    /// which is how rows are read into an anonymous type: the fields go to the parameters of its constructor by
    /// name. The value of <c>proto</c> itself is not used.
    /// </summary>
    public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto)
    {
        return ReadAllAnonymousAsync(reader, proto, null);
    }

    /// <summary>
    /// As the result is enumerated, reads the first field of every row left in the reader as it is, with a database
    /// null as <c>null</c>.
    /// </summary>
    public static IEnumerable<object?> ReadAllFirstColumn(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
    }

    /// <summary>
    /// As the result is enumerated, reads the first field of every row left in the reader as it is, with a database
    /// null as <c>null</c>.
    /// </summary>
    public static IAsyncEnumerable<object?> ReadAllFirstColumnAsync(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
    }

    /// <summary>
    /// As the result is enumerated, reads the first field of every row left in the reader, converted to <c>T</c>
    /// the way members are mapped.
    /// </summary>
    public static IEnumerable<T> ReadAllFirstColumn<T>(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
    }

    /// <summary>
    /// As the result is enumerated, reads the first field of every row left in the reader, converted to <c>T</c>
    /// the way members are mapped.
    /// </summary>
    public static IAsyncEnumerable<T> ReadAllFirstColumnAsync<T>(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
    }

    #endregion

    #region WriteToServer

    internal static IBulkWriter GetBulkWriter(this DbDataReader reader, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(context);

        return context switch
        {
            ScopedContext scopedContext => new DataReaderAttachedToScopedContext(reader, scopedContext),
            ConnectionStringContext connectionStringContext => new DataReaderAttachedToConnectionStringContext(reader, connectionStringContext),
            _ => throw new ArgumentException("Unknown type of context", nameof(context))
        };
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static void WriteToServer(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        reader.GetBulkWriter(context).WriteToServer(destinationTable, bulkOptions, fields);
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions? bulkOptions, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, bulkOptions, cancellationToken, fields);
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, BulkOptions? bulkOptions, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, bulkOptions, fields);
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static void WriteToServer(this DbDataReader reader, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        reader.GetBulkWriter(context).WriteToServer(destinationTable, fields);
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, cancellationToken, fields);
    }

    /// <summary>
    /// Writes the rows of the reader to the table, with the bulk copy of the provider of the context.
    /// </summary>
    /// <remarks>
    /// <c>fields</c> names the fields to write; without it all the fields are written. Fields are matched to the
    /// columns of the table by name.
    /// </remarks>
    public static Task WriteToServerAsync(this DbDataReader reader, DbContext context, string destinationTable, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(destinationTable);

        return reader.GetBulkWriter(context).WriteToServerAsync(destinationTable, fields);
    }

    #endregion

    /// <summary>
    /// The names of the fields of the reader, in their order.
    /// </summary>
    public static IEnumerable<string> GetColumnNames(this IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return Names(reader);

        static IEnumerable<string> Names(IDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string name = reader.GetName(i);
                yield return name;
            }
        }
    }
}
