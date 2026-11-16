namespace Gerakul.FastSql.Common;

/// <summary>
/// One field of a <c>T</c>: its name, its type and how its value is got. The fields of a type are what it gives to
/// the parameters of a command and to the columns of a data reader.
/// </summary>
public class FieldSettings<T>
{
    /// <summary>
    /// The name of the field: a parameter or a column is found by it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The name of the data type that a data reader reports for the field.
    /// </summary>
    public string DataTypeName { get; }

    /// <summary>
    /// The type of the values of the field.
    /// </summary>
    public Type FieldType { get; }

    /// <summary>
    /// Gets the value of the field from an object.
    /// </summary>
    public Func<T, object?> Getter { get; }

    /// <summary>
    /// Tells whether the value of the field of an object is null.
    /// </summary>
    public Func<T, bool> IsNullGetter { get; }

    /// <summary>
    /// True when no null check was given, and null is found out by getting the value.
    /// </summary>
    public bool DetermineNullByValue { get; }

    private FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter, bool determineNullByValue)
    {
        // the type and the name of its data type are not checked: settings without them have always been accepted
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(isNullGetter);

        this.Name = name;
        this.DataTypeName = dataTypeName;
        this.FieldType = fieldType;
        this.Getter = getter;
        this.IsNullGetter = isNullGetter;
        this.DetermineNullByValue = determineNullByValue;
    }

    /// <summary>
    /// Creates a field. Without <c>dataTypeName</c> the name of <c>fieldType</c> is used; without
    /// <c>isNullGetter</c> null is found out by getting the value; with the name alone the field is the object
    /// itself.
    /// </summary>
    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter)
      : this(name, dataTypeName, fieldType, getter, isNullGetter, false)
    {
    }

    /// <summary>
    /// Creates a field. Without <c>dataTypeName</c> the name of <c>fieldType</c> is used; without
    /// <c>isNullGetter</c> null is found out by getting the value; with the name alone the field is the object
    /// itself.
    /// </summary>
    public FieldSettings(string name, Type fieldType, Func<T, object?> getter, Func<T, bool> isNullGetter)
      : this(name, fieldType.Name, fieldType, getter, isNullGetter, false)
    {
    }

    /// <summary>
    /// Creates a field. Without <c>dataTypeName</c> the name of <c>fieldType</c> is used; without
    /// <c>isNullGetter</c> null is found out by getting the value; with the name alone the field is the object
    /// itself.
    /// </summary>
    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object?> getter)
      : this(name, dataTypeName, fieldType, getter, x => getter(x) == null, true)
    {
    }

    /// <summary>
    /// Creates a field. Without <c>dataTypeName</c> the name of <c>fieldType</c> is used; without
    /// <c>isNullGetter</c> null is found out by getting the value; with the name alone the field is the object
    /// itself.
    /// </summary>
    public FieldSettings(string name, Type fieldType, Func<T, object?> getter)
      : this(name, fieldType.Name, fieldType, getter, x => getter(x) == null, true)
    {
    }

    /// <summary>
    /// Creates a field. Without <c>dataTypeName</c> the name of <c>fieldType</c> is used; without
    /// <c>isNullGetter</c> null is found out by getting the value; with the name alone the field is the object
    /// itself.
    /// </summary>
    public FieldSettings(string name)
      : this(name, typeof(T).Name, typeof(T), x => x, x => x == null, true)
    {
    }
}

/// <summary>
/// Creates the field settings of a type from its public fields and properties.
/// </summary>
public static partial class FieldSettings
{
    // The settings of a type are built once (see TypeMembers). Every call returns a new array, which the caller
    // can change; the settings in it are immutable and shared
    /// <summary>
    /// The field settings of <c>T</c>, as <c>fromTypeOption</c> says. The overload with <c>proto</c> takes the type
    /// from the value, which is how an anonymous type is named. Each call returns a new array of the same settings.
    /// </summary>
    public static FieldSettings<T>[] FromType<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return (FieldSettings<T>[])FromTypeShared<T>(fromTypeOption).Clone();
    }

    // the array that is kept for the type: for callers that only read it
    internal static FieldSettings<T>[] FromTypeShared<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return TypeMembers<T>.ForWriting(fromTypeOption).Settings;
    }

    /// <summary>
    /// The field settings of <c>T</c>, as <c>fromTypeOption</c> says. The overload with <c>proto</c> takes the type
    /// from the value, which is how an anonymous type is named. Each call returns a new array of the same settings.
    /// </summary>
    public static FieldSettings<T>[] FromType<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromType<T>(fromTypeOption);
    }

    /// <summary>
    /// The names of the fields of <c>T</c>, as <c>fromTypeOption</c> says.
    /// </summary>
    public static string[] GetFields<T>(FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromTypeShared<T>(fromTypeOption).GetNames();
    }

    /// <summary>
    /// The names of the fields of <c>T</c>, as <c>fromTypeOption</c> says.
    /// </summary>
    public static string[] GetFields<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        return FromTypeShared<T>(fromTypeOption).GetNames();
    }
}

/// <summary>
/// Additions to sequences of field settings.
/// </summary>
public static class FieldSettingsExtension
{
    /// <summary>
    /// The names of the fields.
    /// </summary>
    public static string[] GetNames<T>(this IEnumerable<FieldSettings<T>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.Select(x => x.Name).ToArray();
    }
}
