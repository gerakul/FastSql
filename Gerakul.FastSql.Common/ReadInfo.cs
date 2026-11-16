using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

// the reader field a member's value comes from, and how the value is converted for the member
internal readonly record struct ValueSource(int Ordinal, Type ConvertingType, bool IsNullable, bool IsEnum)
{
    // a reference type or Nullable<T> takes null; the value is converted to the underlying type of Nullable<T>
    public static ValueSource For(int ordinal, Type memberType)
    {
        var underlyingType = Nullable.GetUnderlyingType(memberType);
        var convertingType = underlyingType ?? memberType;
        return new ValueSource(ordinal, convertingType, !memberType.IsValueType || underlyingType != null, convertingType.IsEnum);
    }
}

// reads rows of a reader that is owned by the caller
internal abstract class ReadInfo<T>
{
    public required IDataReader Reader;

    protected object? GetValueFromReader(in ValueSource source)
    {
        if (Reader.IsDBNull(source.Ordinal) && source.IsNullable)
        {
            return null;
        }

        var value = Reader.GetValue(source.Ordinal);

        if (value == null)
        {
            return null;
        }

        if (source.IsEnum)
        {
            if (value is string enumAsString)
            {
                return Enum.Parse(source.ConvertingType, enumAsString);
            }
            else
            {
                return Enum.ToObject(source.ConvertingType, value);
            }
        }

        // a value that fits the member as it is (an object or interface member, an array) is not converted;
        // Convert.ChangeType accepts only IConvertible values for other types than their own
        if (source.ConvertingType.IsInstanceOfType(value))
        {
            return value;
        }

        // Npgsql 10 returns DateOnly and TimeOnly for date and time columns (earlier versions returned DateTime and
        // TimeSpan); Convert.ChangeType cannot convert them, so members of the old types get the old values
        if (value is DateOnly dateOnly && source.ConvertingType == typeof(DateTime))
        {
            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }

        if (value is TimeOnly timeOnly && source.ConvertingType == typeof(TimeSpan))
        {
            return timeOnly.ToTimeSpan();
        }

        // the invariant culture: data must not depend on the machine's settings ("1.5" is 15 under de-DE)
        return Convert.ChangeType(value, source.ConvertingType, CultureInfo.InvariantCulture);
    }

    public abstract T GetValue();
}

// What a query does before its first row. The members of the type come from TypeMembers, where they are found
// once per type; here they are matched with the fields of this reader
internal static class ReadInfoFactory
{
    // the values of the options; the instance of the caller is not changed
    private static void GetOptions(ReadOptions? readOptions, out FieldsSelector fieldsSelector, out FromTypeOption fromTypeOption,
        out bool caseSensitive)
    {
        var defaults = ReadOptions.defaultOptions;
        fieldsSelector = readOptions?.FieldsSelector ?? defaults.FieldsSelector!.Value;
        fromTypeOption = readOptions?.FromTypeOption ?? defaults.FromTypeOption!.Value;
        caseSensitive = readOptions?.CaseSensitiveFieldsMatching ?? defaults.CaseSensitiveFieldsMatching!.Value;
    }

    private static string?[] GetReaderFields(IDataReader reader)
    {
        var readerFields = new string?[reader.FieldCount];

        for (int i = 0; i < readerFields.Length; i++)
        {
            readerFields[i] = reader.GetName(i);
        }

        return readerFields;
    }

    // Finds the reader field of every member: its ordinal, or -1 for a member without one. Returns the number of
    // members that have one
    private static int Match(string?[] readerFields, NameLookup members, bool caseSensitive, Span<int> ordinals)
    {
        if (Helpers.HasDuplicates(readerFields, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Reader field names has been duplicated");
        }

        if (members.HasDuplicates(caseSensitive))
        {
            throw new ArgumentException("Destination object field names has been duplicated");
        }

        ordinals.Fill(-1);
        int matched = 0;

        for (int i = 0; i < readerFields.Length; i++)
        {
            if (members.TryFind(readerFields[i], caseSensitive, out int index))
            {
                ordinals[index] = i;
                matched++;
            }
        }

        return matched;
    }

    public static ReadInfoByType<T> CreateByType<T>(IDataReader reader, ReadOptions? readOptions) where T : new()
    {
        GetOptions(readOptions, out var fieldsSelector, out var fromTypeOption, out bool caseSensitive);
        var readerFields = GetReaderFields(reader);
        var members = TypeMembers<T>.ForReading(fromTypeOption);

        Span<int> ordinals = members.All.Length <= 64 ? stackalloc int[members.All.Length] : new int[members.All.Length];
        int matched = Match(readerFields, members.Names, caseSensitive, ordinals);

        Helpers.CheckFieldSelection(fieldsSelector, readerFields.Length, members.All.Length, matched);

        // in the order of the members: fields first, then properties
        var values = new (ReadMember Member, ValueSource Source)[matched];

        for (int i = 0, n = 0; i < ordinals.Length; i++)
        {
            if (ordinals[i] >= 0)
            {
                values[n++] = (members.All[i], members.All[i].SourceAt(ordinals[i]));
            }
        }

        return new ReadInfoByType<T>()
        {
            Reader = reader,
            members = members,
            values = values
        };
    }

    public static ReadInfoObjects CreateObjects(IDataReader reader)
    {
        return new ReadInfoObjects()
        {
            Reader = reader,
            Len = reader.FieldCount
        };
    }

    public static ReadInfoAnonymous<T> CreateAnonymous<T>(IDataReader reader, T proto, ReadOptions? readOptions)
    {
        GetOptions(readOptions, out var fieldsSelector, out _, out bool caseSensitive);
        var readerFields = GetReaderFields(reader);
        var constructor = TypeMembers<T>.Constructor;

        if (constructor.Constructor == null)
        {
            throw new InvalidOperationException($"Type {typeof(T)} has no public constructor");
        }

        var all = constructor.Parameters;
        Span<int> ordinals = all.Length <= 64 ? stackalloc int[all.Length] : new int[all.Length];
        int matched = Match(readerFields, constructor.Names, caseSensitive, ordinals);

        Helpers.CheckFieldSelection(fieldsSelector, readerFields.Length, all.Length, matched);

        var parameters = new (int Position, ValueSource Source)[matched];

        for (int i = 0, n = 0; i < ordinals.Length; i++)
        {
            if (ordinals[i] >= 0)
            {
                parameters[n++] = (all[i].Position, all[i].SourceAt(ordinals[i]));
            }
        }

        return new ReadInfoAnonymous<T>()
        {
            Reader = reader,
            constructor = constructor,
            parameters = parameters
        };
    }

    public static ReadInfoFirstColumn CreateFirstColumn(IDataReader reader)
    {
        return new ReadInfoFirstColumn()
        {
            Reader = reader
        };
    }

    public static ReadInfoFirstColumn<T> CreateFirstColumn<T>(IDataReader reader)
    {
        return new ReadInfoFirstColumn<T>()
        {
            Reader = reader
        };
    }
}

internal sealed class ReadInfoByType<T> : ReadInfo<T> where T : new()
{
    public required ReadMembers members;
    public required (ReadMember Member, ValueSource Source)[] values;

    // Optimized from the first call, as the other code that runs for every row: the first reads of a type in a new
    // process run before the runtime optimizes the code, and unoptimized calls made them slower than before the
    // members were kept per type
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override T GetValue()
    {
        // boxed once, so the members of a struct are set on the instance that is returned
        object obj = new T();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Member.SetValue(obj, GetValueFromReader(in values[i].Source));
        }

        members.CountRow();

        return (T)obj;
    }
}

internal sealed class ReadInfoObjects : ReadInfo<object[]>
{
    public int Len;

    public override object[] GetValue()
    {
        object[] objects = new object[Len];
        Reader.GetValues(objects);
        return objects;
    }
}

internal sealed class ReadInfoAnonymous<T> : ReadInfo<T>
{
    public required ConstructorMembers constructor;
    public required (int Position, ValueSource Source)[] parameters;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override T GetValue()
    {
        // a parameter without a reader field gets null, which is the default value for a value type
        object?[] args = new object?[constructor.Parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            args[parameters[i].Position] = GetValueFromReader(in parameters[i].Source);
        }

        var value = (T)constructor.Create(args);
        constructor.CountRow();

        return value;
    }
}

internal sealed class ReadInfoFirstColumn : ReadInfo<object?>
{
    public override object? GetValue()
    {
        return Reader.IsDBNull(0) ? null : Reader.GetValue(0);
    }
}

internal sealed class ReadInfoFirstColumn<T> : ReadInfo<T>
{
    // the same for every reader, so it is found once per type
    private static readonly ValueSource source = ValueSource.For(0, typeof(T));

    public override T GetValue()
    {
        return (T)GetValueFromReader(in source)!;
    }
}
