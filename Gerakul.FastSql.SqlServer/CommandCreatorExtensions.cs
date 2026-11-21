using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// The SQL Server forms of commands that Common has for every provider.
/// </summary>
public static partial class CommandCreatorExtensions
{
    /// <summary>
    /// Creates an insert of <c>value</c> that returns the identity value of the inserted row
    /// (<c>scope_identity()</c>, a <c>decimal</c>); read it with <c>ExecuteScalar&lt;int&gt;</c> or
    /// <c>ExecuteScalar&lt;long&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The same as the <c>CreateInsertAndGetID</c> of Common, which works on every context without a cast to
    /// <c>ISqlCommandCreator</c>; kept so that code written for it compiles to the same call.
    /// </remarks>
    public static IWrappedCommand CreateInsertAndGetID<T>(this ISqlCommandCreator creator, string tableName, T value, params string[] ignoreFields)
    {
        return Common.CommandCreatorExtensions.CreateInsertAndGetID(creator, tableName, value, null, ignoreFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the identity value of the inserted row
    /// (<c>scope_identity()</c>, a <c>decimal</c>); read it with <c>ExecuteScalar&lt;int&gt;</c> or
    /// <c>ExecuteScalar&lt;long&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The same as the <c>CreateInsertAndGetID</c> of Common, which works on every context without a cast to
    /// <c>ISqlCommandCreator</c>; kept so that code written for it compiles to the same call.
    /// </remarks>
    public static IWrappedCommand CreateInsertAndGetID<T>(this ISqlCommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
    {
        return Common.CommandCreatorExtensions.CreateInsertAndGetID(creator, tableName, value, queryOptions, ignoreFields);
    }
}
