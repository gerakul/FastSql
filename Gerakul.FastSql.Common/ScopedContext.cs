using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A context that has a connection to run commands on: over a connection or over a transaction.
/// </summary>
public abstract class ScopedContext : DbContext
{
    /// <summary>
    /// Creates a context with copies of the given options.
    /// </summary>
    protected internal ScopedContext(ContextProvider contextProvider,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        : base(contextProvider, queryOptions, bulkOptions, readOptions)
    {
    }

    internal override ICommandCreator GetICommandCreator()
    {
        return new WrappedCommandWithScope(this);
    }

    /// <summary>
    /// Creates a command with the text on the connection of the context and, when the context is over a
    /// transaction, in that transaction.
    /// </summary>
    public abstract DbCommand CreateCommand(string commandText);
}
