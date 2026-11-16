using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// The entry point of a database: creates contexts, and holds the default options and the parts that differ between
/// databases.
/// </summary>
/// <remarks>
/// Use the <c>DefaultInstance</c> of <c>SqlContextProvider</c> or <c>NpgsqlContextProvider</c>, or create an
/// instance to have default options of your own.
/// </remarks>
public abstract class ContextProvider
{
    /// <summary>
    /// The query options that the contexts created by this provider start with.
    /// </summary>
    public abstract QueryOptions QueryOptions { get; }

    /// <summary>
    /// The bulk copy options that the contexts created by this provider start with.
    /// </summary>
    public abstract BulkOptions BulkOptions { get; }

    /// <summary>
    /// The read options that the contexts created by this provider start with.
    /// </summary>
    public virtual ReadOptions ReadOptions { get; } = new ReadOptions();

    /// <summary>
    /// The generator of command text of this database.
    /// </summary>
    public abstract CommandTextGenerator CommandTextGenerator { get; }

    /// <summary>
    /// The compilator of commands of this provider.
    /// </summary>
    public CommandCompilator CommandCompilator { get; }

    /// <summary>
    /// Creates the provider.
    /// </summary>
    public ContextProvider()
    {
        this.CommandCompilator = new CommandCompilator(this);
    }

    /// <summary>
    /// Creates a context over a connection string: each command opens a connection of its own.
    /// </summary>
    public ConnectionStringContext CreateContext(string connectionString)
    {
        return GetConnectionStringContext(connectionString, QueryOptions, BulkOptions, ReadOptions);
    }

    /// <summary>
    /// Creates a context over an open connection.
    /// </summary>
    public ConnectionContext CreateContext(DbConnection connection)
    {
        return GetConnectionContext(connection, QueryOptions, BulkOptions, ReadOptions);
    }

    /// <summary>
    /// Creates a context over a transaction: commands run on its connection, inside it.
    /// </summary>
    public TransactionContext CreateContext(DbTransaction transaction)
    {
        return GetTransactionContext(transaction, QueryOptions, BulkOptions, ReadOptions);
    }

    /// <summary>
    /// Creates the context of this provider over a connection string, with the given options.
    /// </summary>
    protected internal abstract ConnectionStringContext GetConnectionStringContext(string connectionString,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);

    /// <summary>
    /// Creates the context of this provider over a connection, with the given options.
    /// </summary>
    protected internal abstract ConnectionContext GetConnectionContext(DbConnection connection,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);

    /// <summary>
    /// Creates the context of this provider over a transaction, with the given options.
    /// </summary>
    protected internal abstract TransactionContext GetTransactionContext(DbTransaction transaction,
        QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions);

    /// <summary>
    /// Applies the query options to a command. The base implementation sets the timeout.
    /// </summary>
    protected internal virtual void ApplyQueryOptions(DbCommand cmd, QueryOptions? queryOptions)
    {
        if (queryOptions == null)
        {
            return;
        }

        if (queryOptions.CommandTimeoutSeconds.HasValue)
        {
            cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
        }
    }

    /// <summary>
    /// Adds a parameter with the name and the value to the command. Returns the parameter.
    /// </summary>
    protected internal abstract DbParameter AddParamWithValue(DbCommand cmd, string paramName, object? value);

    /// <summary>
    /// The parameter of the name as it is written in a command text, <c>@name</c> for example.
    /// </summary>
    protected internal abstract string GetSqlParameterName(string name);

    /// <summary>
    /// The value that is passed to the database for a null of the type.
    /// </summary>
    protected internal abstract object GetDbNull(Type type);

    /// <summary>
    /// The names of the parameters found in the command text, without the prefix.
    /// </summary>
    protected internal abstract string[] ParamsFromCommandText(string commandText);

    /// <summary>
    /// The names of the parameters of the fields: one per field.
    /// </summary>
    protected internal abstract string[] ParamsFromSettings<T>(IEnumerable<FieldSettings<T>> settings);

    /// <summary>
    /// Creates the bulk copy of this provider, of the reader into the table.
    /// </summary>
    protected internal abstract BulkCopy GetBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields);

    /// <summary>
    /// The options as the options type of this provider. <c>needClone</c> tells that the result is the object
    /// passed in, which is to be cloned before it is changed.
    /// </summary>
    protected internal abstract QueryOptions GetTyped(QueryOptions options, out bool needClone);

    /// <summary>
    /// The options as the options type of this provider. <c>needClone</c> tells that the result is the object
    /// passed in, which is to be cloned before it is changed.
    /// </summary>
    protected internal abstract BulkOptions GetTyped(BulkOptions options, out bool needClone);
}
