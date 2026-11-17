using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.SqlServer;

/// <summary>
/// Marks what SQL Server commands are created on, so that the extension methods of SQL Server alone are offered
/// there.
/// </summary>
public interface ISqlCommandCreator : ICommandCreator
{
}
