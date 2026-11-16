using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// Something commands are created on. The contexts implement it, and the <c>Create...</c> extension methods are
/// defined on it.
/// </summary>
public interface ICommandCreator
{
    /// <summary>
    /// Creates a command from a delegate that builds the <c>DbCommand</c> where the command runs.
    /// </summary>
    IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter);
}
