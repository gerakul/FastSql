namespace Gerakul.FastSql.Common;

/// <summary>
/// Which members of a type are its fields: for the parameters of a command, for the columns of a data reader and
/// for reading rows into objects.
/// </summary>
[Flags]
public enum FromTypeOption : int
{
    /// <summary>
    /// Public fields.
    /// </summary>
    PublicField = 1,

    /// <summary>
    /// Public properties.
    /// </summary>
    PublicProperty = 2,

    /// <summary>
    /// Public fields and properties, except the ones of collection types. The same as <c>Both</c>.
    /// </summary>
    Default = 3,

    /// <summary>
    /// Public fields and properties, except the ones of collection types.
    /// </summary>
    Both = 3,

    /// <summary>
    /// Added to the others: members of collection types (anything enumerable, except strings and sequences of
    /// bytes) are taken as well.
    /// </summary>
    Collection = 4,

    /// <summary>
    /// Public fields and properties, the ones of collection types included.
    /// </summary>
    All = 7
}
