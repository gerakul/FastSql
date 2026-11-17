using System.Data;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

// reads rows of a reader that is owned by the caller
internal abstract class ReadInfo<T>
{
    public required IDataReader Reader;

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

    // Finds the reader field of every member: its ordinal, or -1 for a member without one, and checks the result
    // against the FieldsSelector. Returns the number of members that have a field. what: "member" or
    // "constructor parameter", for the messages
    private static int Match(string?[] readerFields, NameLookup members, string what, Type type, FieldsSelector fieldsSelector, bool caseSensitive,
        Span<int> ordinals)
    {
        if (Helpers.TryFindDuplicate(readerFields, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase, out var duplicate))
        {
            throw new ArgumentException(Messages.DuplicateReaderField(duplicate, caseSensitive));
        }

        if (members.HasDuplicates(caseSensitive, out var first, out var second))
        {
            throw new ArgumentException(Messages.DuplicateMembers(what + "s", first, second, type, caseSensitive));
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

        bool fieldsWithoutMember = readerFields.Length > matched;
        bool membersWithoutField = members.Count > matched;
        bool satisfied = fieldsSelector switch
        {
            FieldsSelector.Source => !fieldsWithoutMember,
            FieldsSelector.Destination => !membersWithoutField,
            FieldsSelector.Common => true,
            FieldsSelector.Both => !fieldsWithoutMember && !membersWithoutField,
            _ => throw new ArgumentException($"Unknown FieldsSelector. Value: {fieldsSelector}")
        };

        if (!satisfied)
        {
            var fields = new List<string?>();
            var names = new List<string?>();

            foreach (var field in readerFields)
            {
                if (!members.TryFind(field, caseSensitive, out _))
                {
                    fields.Add(field);
                }
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (ordinals[i] < 0)
                {
                    names.Add(members[i]);
                }
            }

            throw new InvalidOperationException(Messages.FieldSelection(fieldsSelector, what, type, fields, names));
        }

        return matched;
    }

    // A type with a public constructor without parameters is read by setting its members (every struct has one);
    // any other type, a positional record for example, through its public constructor with the most parameters
    public static ReadInfo<T> Create<T>(IDataReader reader, ReadOptions? readOptions)
    {
        return TypeMembers<T>.HasParameterlessConstructor
            ? CreateByType<T>(reader, readOptions)
            : CreateByConstructor<T>(reader, readOptions, TypeMembers<T>.LargestConstructor);
    }

    private static ReadInfoByType<T> CreateByType<T>(IDataReader reader, ReadOptions? readOptions)
    {
        GetOptions(readOptions, out var fieldsSelector, out var fromTypeOption, out bool caseSensitive);
        var readerFields = GetReaderFields(reader);
        var members = TypeMembers<T>.ForReading(fromTypeOption);

        Span<int> ordinals = members.All.Length <= 64 ? stackalloc int[members.All.Length] : new int[members.All.Length];
        int matched = Match(readerFields, members.Names, "member", typeof(T), fieldsSelector, caseSensitive, ordinals);

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

    public static ReadInfoByConstructor<T> CreateAnonymous<T>(IDataReader reader, T proto, ReadOptions? readOptions)
    {
        return CreateByConstructor<T>(reader, readOptions, TypeMembers<T>.Constructor);
    }

    private static ReadInfoByConstructor<T> CreateByConstructor<T>(IDataReader reader, ReadOptions? readOptions, ConstructorMembers constructor)
    {
        GetOptions(readOptions, out var fieldsSelector, out _, out bool caseSensitive);
        var readerFields = GetReaderFields(reader);

        if (constructor.Constructor == null)
        {
            throw new InvalidOperationException($"Type {typeof(T)} has no public constructor");
        }

        var all = constructor.Parameters;
        Span<int> ordinals = all.Length <= 64 ? stackalloc int[all.Length] : new int[all.Length];
        int matched = Match(readerFields, constructor.Names, "constructor parameter", typeof(T), fieldsSelector, caseSensitive, ordinals);

        var parameters = new (int Position, ValueSource Source)[matched];

        for (int i = 0, n = 0; i < ordinals.Length; i++)
        {
            if (ordinals[i] >= 0)
            {
                parameters[n++] = (all[i].Position, all[i].SourceAt(ordinals[i]));
            }
        }

        return new ReadInfoByConstructor<T>()
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

internal sealed class ReadInfoByType<T> : ReadInfo<T>
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
        object obj = Activator.CreateInstance<T>()!;

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Member.SetValue(obj, ValueConverter.FromReader(Reader, in values[i].Source));
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

internal sealed class ReadInfoByConstructor<T> : ReadInfo<T>
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
            args[parameters[i].Position] = ValueConverter.FromReader(Reader, in parameters[i].Source);
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
        return (T)ValueConverter.FromReader(Reader, in source)!;
    }
}
