namespace Gerakul.FastSql.Common;

/// <summary>
/// Which names must have a counterpart when fields are matched by name. In reading the source is the fields of the
/// reader and the destination is the members of the type; in a bulk copy the source is the fields being written and
/// the destination is the columns of the table.
/// </summary>
/// <remarks>
/// A name without a counterpart is an <c>InvalidOperationException</c> when the selector requires one, and is left
/// out when it does not.
/// </remarks>
public enum FieldsSelector
{
    /// <summary>
    /// Every name of the source must have a counterpart.
    /// </summary>
    Source = 1,

    /// <summary>
    /// Every name of the destination must have a counterpart.
    /// </summary>
    Destination = 2,

    /// <summary>
    /// Nothing is required: the names that both sides have are used.
    /// </summary>
    Common = 3,

    /// <summary>
    /// Every name of the source and of the destination must have a counterpart.
    /// </summary>
    Both = 4
}
