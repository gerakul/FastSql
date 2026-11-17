using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A forward-only data reader over a sequence of objects: every object is a row, every field setting is a field.
/// <c>ToDataReader</c> creates it, and a bulk copy reads it.
/// </summary>
public class UniversalDataReader<T> : DbDataReader
{
    private readonly IEnumerator<T>? enumerator;
    private readonly IAsyncEnumerator<T>? asyncEnumerator;
    private bool isClosed;
    private T curRow = default!;
    private readonly int fieldCount;
    private Func<T, object?>[] getters;
    private Func<T, bool>[] isNullGetters;
    private readonly string[] dataTypeNames;
    private readonly Type[] types;
    private readonly string[] names;
    private readonly Dictionary<string, int> ordinals;

    private readonly FieldSettings<T>[] settings;
    private ReadOnlyCollection<FieldSettings<T>>? settingsCollection;

    // set when the settings are the ones FastSql keeps for the type, until their getters are the compiled ones
    private WriteMembers<T>? typeMembers;

    /// <summary>
    /// The fields of the reader, in their order.
    /// </summary>
    public ReadOnlyCollection<FieldSettings<T>> Settings => settingsCollection ??= new ReadOnlyCollection<FieldSettings<T>>(settings);

    private UniversalDataReader(IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(settings);

        this.settings = settings.ToArray();
        this.fieldCount = this.settings.Length;

        this.getters = new Func<T, object?>[this.fieldCount];
        this.isNullGetters = new Func<T, bool>[this.fieldCount];
        this.dataTypeNames = new string[this.fieldCount];
        this.types = new Type[this.fieldCount];
        this.names = new string[this.fieldCount];
        this.ordinals = new Dictionary<string, int>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < this.fieldCount; i++)
        {
            var item = this.settings[i];
            this.getters[i] = item.Getter;
            this.isNullGetters[i] = item.IsNullGetter;
            this.dataTypeNames[i] = item.DataTypeName;
            this.types[i] = item.FieldType;
            this.names[i] = item.Name;

            if (!this.ordinals.TryAdd(item.Name, i))
            {
                throw new ArgumentException($"Field '{item.Name}' is defined more than once{Messages.ComparedIgnoringCase(caseSensitive)}", nameof(settings));
            }
        }
    }

    /// <summary>
    /// Creates a reader over the sequence with the given fields. <c>caseSensitive</c> is about finding a field by
    /// its name; two fields of one name are an <c>ArgumentException</c>.
    /// </summary>
    public UniversalDataReader(IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
      : this(settings, caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        this.enumerator = values.GetEnumerator();
    }

    /// <summary>
    /// Creates a reader over the sequence with the given fields. <c>caseSensitive</c> is about finding a field by
    /// its name; two fields of one name are an <c>ArgumentException</c>.
    /// </summary>
    public UniversalDataReader(IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
       : this(settings, caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        this.asyncEnumerator = values.GetAsyncEnumerator();
    }

    // With the settings FastSql keeps for the type. Their getters go through reflection until many rows of the
    // type have been written; this reader counts its rows for that and takes the compiled getters when they are there
    internal UniversalDataReader(IEnumerable<T> values, WriteMembers<T> typeMembers, bool caseSensitive)
      : this(values, typeMembers.Settings, caseSensitive)
    {
        this.typeMembers = typeMembers.IsFast ? null : typeMembers;
    }

    internal UniversalDataReader(IAsyncEnumerable<T> values, WriteMembers<T> typeMembers, bool caseSensitive)
       : this(values, typeMembers.Settings, caseSensitive)
    {
        this.typeMembers = typeMembers.IsFast ? null : typeMembers;
    }

    private void CountRow()
    {
        if (typeMembers != null && typeMembers.CountRow())
        {
            // the same members in the same order, with compiled getters
            var fastSettings = typeMembers.Settings;
            var fastGetters = new Func<T, object?>[fieldCount];
            var fastIsNullGetters = new Func<T, bool>[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                fastGetters[i] = fastSettings[i].Getter;
                fastIsNullGetters[i] = fastSettings[i].IsNullGetter;
            }

            getters = fastGetters;
            isNullGetters = fastIsNullGetters;
            typeMembers = null;
        }
    }

    #region DbDataReader

    // Closes the reader and disposes the source, which can hold resources: a FastSql query keeps its reader and its
    // connection. Dispose, DisposeAsync and CloseAsync of the base class all come here, whatever the type of the
    // reference they are called through
    /// <inheritdoc />
    public override void Close()
    {
        if (!isClosed)
        {
            isClosed = true;
            enumerator?.Dispose();
            asyncEnumerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // disposes an async source without blocking on it, unlike Dispose
    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (!isClosed)
        {
            isClosed = true;
            enumerator?.Dispose();

            if (asyncEnumerator != null)
            {
                await asyncEnumerator.DisposeAsync().ConfigureAwait(false);
            }
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override int Depth => 0;

    /// <inheritdoc />
    public override bool IsClosed => isClosed;

    // there is only one result set
    /// <inheritdoc />
    public override bool NextResult()
    {
        return false;
    }

    /// <inheritdoc />
    public override bool Read()
    {
        if (isClosed)
        {
            throw new InvalidOperationException("DataReader has been closed");
        }

        // one of the two sources is set
        if (asyncEnumerator != null)
        {
            // a move that has already completed (buffered rows, in-memory sources) is read without a task
            var move = asyncEnumerator.MoveNextAsync();
            if (move.IsCompletedSuccessfully ? move.Result : move.AsTask().GetAwaiter().GetResult())
            {
                curRow = asyncEnumerator.Current;
                CountRow();
                return true;
            }
        }
        else
        {
            if (enumerator!.MoveNext())
            {
                curRow = enumerator.Current;
                CountRow();
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (isClosed)
        {
            throw new InvalidOperationException("DataReader has been closed");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // one of the two sources is set
        if (asyncEnumerator != null)
        {
            if (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                curRow = asyncEnumerator.Current;
                CountRow();
                return true;
            }
        }
        else
        {
            if (enumerator!.MoveNext())
            {
                curRow = enumerator.Current;
                CountRow();
                return true;
            }
        }

        return false;
    }

    // -1, as for a select statement
    /// <inheritdoc />
    public override int RecordsAffected => -1;

    /// <inheritdoc />
    public override int FieldCount => fieldCount;

    /// <inheritdoc />
    public override bool HasRows => throw new NotImplementedException();

    /// <inheritdoc />
    public override bool GetBoolean(int i)
    {
        return (bool)Value(i);
    }

    /// <inheritdoc />
    public override byte GetByte(int i)
    {
        return (byte)Value(i);
    }

    /// <inheritdoc />
    public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length)
    {
        byte[] bytes = (byte[])Value(i);

        // without a buffer the length of the value is returned (IDataRecord contract)
        if (buffer == null)
        {
            return bytes.Length;
        }

        if (fieldOffset < 0 || fieldOffset >= bytes.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
        {
            return 0;
        }

        int fo = checked((int)fieldOffset);

        int len = Math.Min(Math.Min(bytes.Length - fo, buffer.Length - bufferOffset), length);
        Array.Copy(bytes, fo, buffer, bufferOffset, len);

        return len;
    }

    /// <inheritdoc />
    public override char GetChar(int i)
    {
        return (char)Value(i);
    }

    /// <inheritdoc />
    public override long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length)
    {
        var value = Value(i);

        // a string is read in place, not copied as a whole on every call
        ReadOnlySpan<char> chars = value is string str ? str.AsSpan() : (char[])value;

        // without a buffer the length of the value is returned (IDataRecord contract)
        if (buffer == null)
        {
            return chars.Length;
        }

        if (fieldOffset < 0 || fieldOffset >= chars.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
        {
            return 0;
        }

        int fo = checked((int)fieldOffset);

        int len = Math.Min(Math.Min(chars.Length - fo, buffer.Length - bufferOffset), length);
        chars.Slice(fo, len).CopyTo(buffer.AsSpan(bufferOffset));

        return len;
    }

    /// <inheritdoc />
    public override string GetDataTypeName(int i)
    {
        return dataTypeNames[i];
    }

    /// <inheritdoc />
    public override DateTime GetDateTime(int i)
    {
        return (DateTime)Value(i);
    }

    /// <inheritdoc />
    public override decimal GetDecimal(int i)
    {
        return (decimal)Value(i);
    }

    /// <inheritdoc />
    public override double GetDouble(int i)
    {
        return (double)Value(i);
    }

    /// <inheritdoc />
    public override Type GetFieldType(int i)
    {
        return types[i];
    }

    /// <inheritdoc />
    public override float GetFloat(int i)
    {
        return (float)Value(i);
    }

    /// <inheritdoc />
    public override Guid GetGuid(int i)
    {
        return (Guid)Value(i);
    }

    /// <inheritdoc />
    public override short GetInt16(int i)
    {
        return (short)Value(i);
    }

    /// <inheritdoc />
    public override int GetInt32(int i)
    {
        return (int)Value(i);
    }

    /// <inheritdoc />
    public override long GetInt64(int i)
    {
        return (long)Value(i);
    }

    /// <inheritdoc />
    public override string GetName(int i)
    {
        return names[i];
    }

    /// <inheritdoc />
    public override int GetOrdinal(string name)
    {
        // IDataRecord documents IndexOutOfRangeException for an unknown name
        if (!this.ordinals.TryGetValue(name, out int ordinal))
        {
            throw new IndexOutOfRangeException($"Field '{name}' is not found");
        }

        return ordinal;
    }

    /// <inheritdoc />
    public override string GetString(int i)
    {
        return (string)Value(i);
    }

    /// <inheritdoc />
    public override object GetValue(int i)
    {
        return Value(i);
    }

    // null for a null value (IsDBNull is true), as it always was, although DbDataReader declares non-null values
    private object Value(int i)
    {
        return getters[i](curRow)!;
    }

    /// <inheritdoc />
    public override int GetValues(object[] values)
    {
        int res = Math.Min(values.Length, fieldCount);
        for (int i = 0; i < res; i++)
        {
            values[i] = Value(i);
        }

        return res;
    }

    /// <inheritdoc />
    public override bool IsDBNull(int i)
    {
        return isNullGetters[i](curRow);
    }

    /// <inheritdoc />
    public override IEnumerator GetEnumerator()
    {
        return enumerator!; // null for an async source, as before
    }

    /// <inheritdoc />
    public override object this[string name] => Value(GetOrdinal(name));

    /// <inheritdoc />
    public override object this[int i] => Value(i);

    #endregion
}
