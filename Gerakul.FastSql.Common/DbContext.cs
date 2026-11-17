using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// The base of the contexts. A context is where commands run (a connection string, a connection or a transaction)
/// and the options they run with. The <c>Create...</c> extension methods create commands on it.
/// </summary>
public abstract class DbContext : ICommandCreator
{
    /// <summary>
    /// The provider of the context.
    /// </summary>
    public ContextProvider ContextProvider { get; }

    /// <summary>
    /// The compilator of commands of the provider.
    /// </summary>
    public CommandCompilator CommandCompilator => ContextProvider.CommandCompilator;

    /// <summary>
    /// The query options of the context: what its commands take for the values their own options do not set.
    /// </summary>
    /// <remarks>
    /// On a connection string context they are read each time a command runs; on a connection or a transaction
    /// context, when the command is created.
    /// </remarks>
    public QueryOptions QueryOptions { get; }

    /// <summary>
    /// The bulk copy options of the context: what a bulk copy takes for the values its own options do not set.
    /// </summary>
    /// <remarks>
    /// They are read when <c>WriteToServer</c> runs.
    /// </remarks>
    public BulkOptions BulkOptions { get; }

    /// <summary>
    /// The read options of the context: what reading rows takes for the values its own options do not set.
    /// </summary>
    /// <remarks>
    /// On a connection string context they are read when the result of a query is enumerated; on a connection or a
    /// transaction context, when the reading method (<c>ExecuteQuery</c> and the like) is called.
    /// </remarks>
    public ReadOptions ReadOptions { get; }

    /// <summary>
    /// Creates a context with copies of the given options.
    /// </summary>
    protected internal DbContext(ContextProvider contextProvider,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
    {
        ArgumentNullException.ThrowIfNull(contextProvider);
        ArgumentNullException.ThrowIfNull(queryOptions);
        ArgumentNullException.ThrowIfNull(bulkOptions);
        ArgumentNullException.ThrowIfNull(readOptions);

        this.ContextProvider = contextProvider;
        this.QueryOptions = queryOptions.Clone();
        this.BulkOptions = bulkOptions.Clone();
        this.ReadOptions = readOptions.Clone();
    }

    internal QueryOptions PrepareQueryOptions(QueryOptions? options)
    {
        if (options == null)
        {
            return QueryOptions.Clone().SetDefaults();
        }

        var opt = ContextProvider.GetTyped(options, out var needClone);
        if (needClone)
        {
            opt = opt.Clone();
        }

        return opt.SetDefaults(QueryOptions).SetDefaults();
    }

    internal BulkOptions PrepareBulkOptions(BulkOptions? options)
    {
        if (options == null)
        {
            return BulkOptions.Clone().SetDefaults();
        }

        var opt = ContextProvider.GetTyped(options, out var needClone);
        if (needClone)
        {
            opt = opt.Clone();
        }

        return opt.SetDefaults(BulkOptions).SetDefaults();
    }

    internal ReadOptions PrepareReadOptions(ReadOptions? options)
    {
        if (options == null)
        {
            return ReadOptions.Clone();
        }

        var opt = options.Clone();
        opt.SetDefaults(ReadOptions);
        return opt;
    }

    internal abstract ICommandCreator GetICommandCreator();

    #region ICommandCreator

    /// <summary>
    /// Creates a command from a delegate that builds the <c>DbCommand</c> where the command runs. The
    /// <c>Create...</c> methods go through it.
    /// </summary>
    public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
    {
        ArgumentNullException.ThrowIfNull(commandGetter);

        return GetICommandCreator().Set(commandGetter);
    }

    #endregion
}
