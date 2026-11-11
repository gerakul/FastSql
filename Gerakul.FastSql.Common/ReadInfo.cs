using System.Data;
using System.Globalization;
using System.Reflection;

namespace Gerakul.FastSql.Common;

internal readonly record struct ReaderField(int Ordinal, string Name);

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

internal static class ReadInfoFactory
{
    private static ReadOptions GetReadOptions(ReadOptions? readOptions)
    {
        if (readOptions == null)
        {
            return new ReadOptions(true);
        }
        else
        {
            return readOptions.Clone().SetDefaults();
        }
    }

    private static StringComparer GetComparer(ReadOptions readOptions)
    {
        return readOptions.CaseSensitiveFieldsMatching!.Value ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
    }

    private static ReaderField[] GetReaderFields(IDataReader reader)
    {
        var readerFields = new ReaderField[reader.FieldCount];

        for (int i = 0; i < readerFields.Length; i++)
        {
            readerFields[i] = new ReaderField(i, reader.GetName(i));
        }

        return readerFields;
    }

    public static ReadInfoByType<T> CreateByType<T>(IDataReader reader, ReadOptions? readOptions) where T : new()
    {
        ReadOptions readOpt = GetReadOptions(readOptions);
        var readerFields = GetReaderFields(reader);

        Helpers.GetMembers(typeof(T), readOpt.FromTypeOption!.Value, true, out FieldInfo[] fiAll, out PropertyInfo[] piAll);

        var comparer = GetComparer(readOpt);

        if (readerFields.GroupBy(x => x.Name, comparer).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Reader field names has been duplicated");
        }

        if (piAll.Select(x => x.Name).Concat(fiAll.Select(x => x.Name)).GroupBy(x => x, comparer).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Destination object field names has been duplicated");
        }

        var fields = fiAll.Join(readerFields, x => x.Name, x => x.Name,
            (x, y) => (Field: x, Source: ValueSource.For(y.Ordinal, x.FieldType)), comparer).ToArray();
        var properties = piAll.Join(readerFields, x => x.Name, x => x.Name,
            (x, y) => (Property: x, Source: ValueSource.For(y.Ordinal, x.PropertyType)), comparer).ToArray();

        Helpers.CheckFieldSelection(readOpt.FieldsSelector!.Value, readerFields.Length, fiAll.Length + piAll.Length, fields.Length + properties.Length);

        return new ReadInfoByType<T>()
        {
            Reader = reader,
            fields = fields,
            properties = properties
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
        ReadOptions readOpt = GetReadOptions(readOptions);
        var readerFields = GetReaderFields(reader);

        ConstructorInfo? constructor = typeof(T).GetConstructors().FirstOrDefault();
        if (constructor == null)
        {
            throw new InvalidOperationException($"Type {typeof(T)} has no public constructor");
        }

        ParameterInfo[] piAll = constructor.GetParameters();

        var comparer = GetComparer(readOpt);

        if (readerFields.GroupBy(x => x.Name, comparer).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Reader field names has been duplicated");
        }

        if (piAll.GroupBy(x => x.Name, comparer).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Destination object field names has been duplicated");
        }

        var parameters = piAll.Join(readerFields, x => x.Name, x => x.Name,
            (x, y) => (x.Position, Source: ValueSource.For(y.Ordinal, x.ParameterType)), comparer).ToArray();

        Helpers.CheckFieldSelection(readOpt.FieldsSelector!.Value, readerFields.Length, piAll.Length, parameters.Length);

        return new ReadInfoAnonymous<T>()
        {
            Reader = reader,
            constructor = constructor,
            parameterCount = piAll.Length,
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
            Reader = reader,
            source = ValueSource.For(0, typeof(T))
        };
    }
}

internal sealed class ReadInfoByType<T> : ReadInfo<T> where T : new()
{
    public required (FieldInfo Field, ValueSource Source)[] fields;
    public required (PropertyInfo Property, ValueSource Source)[] properties;

    public override T GetValue()
    {
        // boxed once, so the members of a struct are set on the instance that is returned
        object obj = new T();

        for (int i = 0; i < fields.Length; i++)
        {
            fields[i].Field.SetValue(obj, GetValueFromReader(in fields[i].Source));
        }

        for (int i = 0; i < properties.Length; i++)
        {
            properties[i].Property.SetValue(obj, GetValueFromReader(in properties[i].Source));
        }

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
    public required ConstructorInfo constructor;
    public int parameterCount;
    public required (int Position, ValueSource Source)[] parameters;

    public override T GetValue()
    {
        object?[] args = new object?[parameterCount];

        for (int i = 0; i < parameters.Length; i++)
        {
            args[parameters[i].Position] = GetValueFromReader(in parameters[i].Source);
        }

        return (T)constructor.Invoke(args);
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
    public ValueSource source;

    public override T GetValue()
    {
        return (T)GetValueFromReader(in source)!;
    }
}
