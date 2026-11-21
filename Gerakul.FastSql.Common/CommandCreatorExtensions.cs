using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// Creates commands on a context: text commands, calls of stored procedures, and the insert, update, delete and
/// merge of a value.
/// </summary>
/// <remarks>
/// A command runs when one of the methods of <c>IWrappedCommand</c> is called. Table names and command texts go to
/// the server as they are written: never build them from input you do not trust.
/// </remarks>
public static partial class CommandCreatorExtensions
{
    /// <summary>
    /// Creates a text command. The values of <c>parameters</c> are passed by position, as <c>@p0</c>, <c>@p1</c>
    /// and so on.
    /// </summary>
    public static IWrappedCommand CreateSimple(this ICommandCreator creator, string commandText, params object?[] parameters)
    {
        return creator.CreateSimple(null, commandText, parameters);
    }

    /// <summary>
    /// Creates a text command. The values of <c>parameters</c> are passed by position, as <c>@p0</c>, <c>@p1</c>
    /// and so on.
    /// </summary>
    public static IWrappedCommand CreateSimple(this ICommandCreator creator, QueryOptions? queryOptions, string commandText, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(commandText);

        return creator.Set(x => x.CommandCompilator.CompileSimple(commandText).Create(x, queryOptions, parameters));
    }

    /// <summary>
    /// Creates a text command. The values of <c>parameters</c> are passed by position, as <c>@p0</c>, <c>@p1</c>
    /// and so on.
    /// </summary>
    public static IWrappedCommand CreateSimple(this ICommandCreator creator, SimpleCommand precompiledCommand, params object?[] parameters)
    {
        return creator.CreateSimple(null, precompiledCommand, parameters);
    }

    /// <summary>
    /// Creates a text command. The values of <c>parameters</c> are passed by position, as <c>@p0</c>, <c>@p1</c>
    /// and so on.
    /// </summary>
    public static IWrappedCommand CreateSimple(this ICommandCreator creator, QueryOptions? queryOptions, SimpleCommand precompiledCommand, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(precompiledCommand);

        return creator.Set(x => precompiledCommand.Create(x, queryOptions, parameters));
    }

    /// <summary>
    /// Creates a call of a stored procedure with ready-made parameters.
    /// </summary>
    public static IWrappedCommand CreateProcedureSimple(this ICommandCreator creator, string name, params DbParameter[] parameters)
    {
        return creator.CreateProcedureSimple(null, name, parameters);
    }

    /// <summary>
    /// Creates a call of a stored procedure with ready-made parameters.
    /// </summary>
    public static IWrappedCommand CreateProcedureSimple(this ICommandCreator creator, QueryOptions? queryOptions, string name, params DbParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(name);

        return creator.Set(x => x.CommandCompilator.CompileProcedureSimple(name).Create(x, queryOptions, parameters));
    }


    /// <summary>
    /// Creates a text command whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, T value, QueryOptions? queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(commandText);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileMapped<T>(commandText, fromTypeOption, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a text command whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, MappedCommand<T> precompiledCommand, T value, QueryOptions? queryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(precompiledCommand);

        return creator.Set(x => precompiledCommand.Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates a text command whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<string>? paramNames, T value, QueryOptions? queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(commandText);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileMapped<T>(commandText, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a text command whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions? queryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(commandText);
        ArgumentNullException.ThrowIfNull(settings);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileMapped(commandText, settings, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a text command whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateMapped<T>(this ICommandCreator creator, string commandText, IList<string>? paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions? queryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(commandText);
        ArgumentNullException.ThrowIfNull(settings);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileMapped(commandText, paramNames, settings, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a call of a stored procedure whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, T value, QueryOptions? queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(name);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileProcedure<T>(name, fromTypeOption, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a call of a stored procedure whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<string>? paramNames, T value, QueryOptions? queryOptions = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(name);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileProcedure<T>(name, paramNames, fromTypeOption, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a call of a stored procedure whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<FieldSettings<T>> settings, T value, QueryOptions? queryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(settings);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileProcedure(name, settings, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates a call of a stored procedure whose parameters are filled from the fields of <c>value</c> by name.
    /// </summary>
    public static IWrappedCommand CreateProcedure<T>(this ICommandCreator creator, string name, IList<string>? paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions? queryOptions = null)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(settings);

        return creator.Set(x =>
        {
            var o = x.PrepareQueryOptions(queryOptions);
            return x.CommandCompilator.CompileProcedure(name, paramNames, settings, o.CaseSensitiveParamsMatching!.Value).CreatePrepared(x, value, o);
        });
    }

    /// <summary>
    /// Creates an insert of <c>value</c> into the table: a column per field, except <c>ignoreFields</c>.
    /// </summary>
    public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
    {
        return creator.CreateInsert(tableName, value, null, ignoreFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> into the table: a column per field, except <c>ignoreFields</c>.
    /// </summary>
    public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileInsert<T>(tableName, ignoreFields).Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the generated ID of the inserted row (its identity or
    /// <c>serial</c> column): a column per field, except <c>ignoreFields</c> (usually the ID itself). Read it with
    /// <c>ExecuteScalar&lt;long&gt;</c> or <c>ExecuteScalar&lt;int&gt;</c>.
    /// </summary>
    /// <remarks>
    /// SQL Server returns <c>scope_identity()</c>, a <c>decimal</c>; PostgreSQL the inserted row's value, a
    /// <c>bigint</c>. A table without such a column gives <c>DBNull</c> on both.
    /// </remarks>
    public static IWrappedCommand CreateInsertAndGetID<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
    {
        return creator.CreateInsertAndGetID(tableName, value, null, ignoreFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the generated ID of the inserted row (its identity or
    /// <c>serial</c> column): a column per field, except <c>ignoreFields</c> (usually the ID itself). Read it with
    /// <c>ExecuteScalar&lt;long&gt;</c> or <c>ExecuteScalar&lt;int&gt;</c>.
    /// </summary>
    /// <remarks>
    /// SQL Server returns <c>scope_identity()</c>, a <c>decimal</c>; PostgreSQL the inserted row's value, a
    /// <c>bigint</c>. A table without such a column gives <c>DBNull</c> on both.
    /// </remarks>
    public static IWrappedCommand CreateInsertAndGetID<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileInsertAndGetID<T>(tableName, ignoreFields).Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the <c>outputFields</c> of the inserted row; read them with
    /// one of the <c>ExecuteQuery</c> methods.
    /// </summary>
    public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, params string[] outputFields)
    {
        return creator.CreateInsertWithOutput(tableName, value, null, null, outputFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the <c>outputFields</c> of the inserted row; read them with
    /// one of the <c>ExecuteQuery</c> methods.
    /// </summary>
    public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] outputFields)
    {
        return creator.CreateInsertWithOutput(tableName, value, queryOptions, null, outputFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the <c>outputFields</c> of the inserted row; read them with
    /// one of the <c>ExecuteQuery</c> methods.
    /// </summary>
    public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, IList<string>? ignoreFields, params string[] outputFields)
    {
        return creator.CreateInsertWithOutput(tableName, value, null, ignoreFields, outputFields);
    }

    /// <summary>
    /// Creates an insert of <c>value</c> that returns the <c>outputFields</c> of the inserted row; read them with
    /// one of the <c>ExecuteQuery</c> methods.
    /// </summary>
    public static IWrappedCommand CreateInsertWithOutput<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, IList<string>? ignoreFields, params string[] outputFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileInsertWithOutput<T>(tableName, FromTypeOption.Default, ignoreFields, outputFields).Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates an update of the row of <c>value</c>: the fields in <c>keyFields</c> find the row, the others are
    /// set.
    /// </summary>
    public static IWrappedCommand CreateUpdate<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateUpdate(tableName, value, null, keyFields);
    }

    /// <summary>
    /// Creates an update of the row of <c>value</c>: the fields in <c>keyFields</c> find the row, the others are
    /// set.
    /// </summary>
    public static IWrappedCommand CreateUpdate<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileUpdate<T>(tableName, keyFields).Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates a delete of the row of <c>value</c>: the fields in <c>keyFields</c> find the row.
    /// </summary>
    public static IWrappedCommand CreateDelete<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateDelete(tableName, value, null, keyFields);
    }

    /// <summary>
    /// Creates a delete of the row of <c>value</c>: the fields in <c>keyFields</c> find the row.
    /// </summary>
    public static IWrappedCommand CreateDelete<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileDelete<T>(tableName, keyFields).Create(x, value, queryOptions));
    }

    /// <summary>
    /// Creates an insert or update of <c>value</c> by <c>keyFields</c>.
    /// </summary>
    public static IWrappedCommand CreateMerge<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateMerge(tableName, value, null, keyFields);
    }

    /// <summary>
    /// Creates an insert or update of <c>value</c> by <c>keyFields</c>.
    /// </summary>
    public static IWrappedCommand CreateMerge<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(tableName);

        return creator.Set(x => x.CommandCompilator.CompileMerge<T>(tableName, keyFields).Create(x, value, queryOptions));
    }

    #region Some combination of Create and Execute

    // each returns the number of rows the command affected, as ExecuteNonQuery does

    /// <summary>
    /// Inserts <c>value</c> into the table. Returns the number of rows affected.
    /// </summary>
    public static int Insert<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
    {
        return creator.CreateInsert(tableName, value, ignoreFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts <c>value</c> into the table. Returns the number of rows affected.
    /// </summary>
    public static int Insert<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
    {
        return creator.CreateInsert(tableName, value, queryOptions, ignoreFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts <c>value</c> into the table. Returns the number of rows affected.
    /// </summary>
    public static Task<int> InsertAsync<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
    {
        return creator.CreateInsert(tableName, value, ignoreFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts <c>value</c> into the table. Returns the number of rows affected.
    /// </summary>
    public static Task<int> InsertAsync<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] ignoreFields)
    {
        return creator.CreateInsert(tableName, value, queryOptions, ignoreFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Update<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateUpdate(tableName, value, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Updates the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Update<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateUpdate(tableName, value, queryOptions, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Updates the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> UpdateAsync<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateUpdate(tableName, value, keyFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> UpdateAsync<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateUpdate(tableName, value, queryOptions, keyFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Delete<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateDelete(tableName, value, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Delete<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateDelete(tableName, value, queryOptions, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> DeleteAsync<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateDelete(tableName, value, keyFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes the row of <c>value</c> found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> DeleteAsync<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateDelete(tableName, value, queryOptions, keyFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts <c>value</c>, or updates its row found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Merge<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateMerge(tableName, value, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts <c>value</c>, or updates its row found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static int Merge<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateMerge(tableName, value, queryOptions, keyFields).ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts <c>value</c>, or updates its row found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> MergeAsync<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
    {
        return creator.CreateMerge(tableName, value, keyFields).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts <c>value</c>, or updates its row found by <c>keyFields</c>. Returns the number of rows affected.
    /// </summary>
    public static Task<int> MergeAsync<T>(this ICommandCreator creator, string tableName, T value, QueryOptions? queryOptions, params string[] keyFields)
    {
        return creator.CreateMerge(tableName, value, queryOptions, keyFields).ExecuteNonQueryAsync();
    }

    #endregion
}
