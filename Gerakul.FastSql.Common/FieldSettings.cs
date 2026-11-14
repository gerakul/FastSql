namespace Gerakul.FastSql.Common;

public class FieldSettings<T>
{
    public string Name { get; }
    public string DataTypeName { get; }
    public Type FieldType { get; }
    public Func<T, object?> Getter { get; }
    public Func<T, bool> IsNullGetter { get; }
    public bool DetermineNullByValue { get; }

    private FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter, bool determineNullByValue)
    {
        this.Name = name;
        this.DataTypeName = dataTypeName;
        this.FieldType = fieldType;
        this.Getter = getter;
        this.IsNullGetter = isNullGetter;
        this.DetermineNullByValue = determineNullByValue;
    }

    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter)
      : this(name, dataTypeName, fieldType, getter, isNullGetter, false)
    {
    }

    public FieldSettings(string name, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter)
      : this(name, fieldType.Name, fieldType, getter, isNullGetter, false)
    {
    }

    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter)
      : this(name, dataTypeName, fieldType, getter, x => getter(x) == null, true)
    {
    }

    public FieldSettings(string name, Type fieldType, Func<T, object?> getter)
      : this(name, fieldType.Name, fieldType, getter, x => getter(x) == null, true)
    {
    }

    public FieldSettings(string name)
      : this(name, typeof(T).Name, typeof(T), x => x, x => x == null, true)
    {
    }
}

public static partial class FieldSettings
{
    // The settings of a type are built once (see TypeMembers). Every call returns a new array, which the caller
    // can change; the settings in it are immutable and shared
    public static FieldSettings<T>[] FromType<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return (FieldSettings<T>[])FromTypeShared<T>(fromTypeOption).Clone();
    }

    // the array that is kept for the type: for callers that only read it
    internal static FieldSettings<T>[] FromTypeShared<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return TypeMembers<T>.ForWriting(fromTypeOption).Settings;
    }

    public static FieldSettings<T>[] FromType<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromType<T>(fromTypeOption);
    }

    public static string[] GetFields<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromTypeShared<T>(fromTypeOption).GetNames();
    }

    public static string[] GetFields<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromTypeShared<T>(fromTypeOption).GetNames();
    }
}

public static class FieldSettingsExtension
{
    public static string[] GetNames<T>(this IEnumerable<FieldSettings<T>> values)
    {
        return values.Select(x => x.Name).ToArray();
    }
}
