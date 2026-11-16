using System.Data;

namespace Gerakul.FastSql.Common;

// The methods that take the type's members from the type (not settings of the caller) keep the command they
// compiled last, per type and kind of command, and return it again when they are called with the same
// arguments: see CommandCache
public class CommandCompilator
{
    private readonly ContextProvider contextProvider;

    public CommandCompilator(ContextProvider contextProvider)
    {
        this.contextProvider = contextProvider;
    }

    #region Simple

    public SimpleCommand CompileSimple(string commandText)
    {
        return new SimpleCommand(contextProvider, commandText);
    }

    public SimpleCommand CompileProcedureSimple(string name)
    {
        return new SimpleCommand(contextProvider, name, CommandType.StoredProcedure);
    }

    #endregion

    #region Mapped

    public MappedCommand<T> CompileMapped<T>(string commandText, IList<string>? paramNames, IList<FieldSettings<T>> settings,
        bool caseSensitiveParamsMatching = false)
    {
        return new MappedCommand<T>(contextProvider, commandText, paramNames, settings, caseSensitiveParamsMatching);
    }

    public MappedCommand<T> CompileMapped<T>(string commandText, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
    {
        return new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText), settings, caseSensitiveParamsMatching);
    }

    public MappedCommand<T> CompileMapped<T>(string commandText, IList<string>? paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default,
        bool caseSensitiveParamsMatching = false)
    {
        // no names means no parameters; in the cache a missing list stands for the names found in the text
        paramNames ??= Array.Empty<string>();

        if (Find(CommandCache<T>.Mapped, commandText, fromTypeOption, caseSensitiveParamsMatching, paramNames, null) is { } last)
        {
            return last;
        }

        var command = new MappedCommand<T>(contextProvider, commandText, paramNames, FieldSettings.FromTypeShared<T>(fromTypeOption), caseSensitiveParamsMatching);
        CommandCache<T>.Mapped = Keep(command, commandText, fromTypeOption, caseSensitiveParamsMatching, paramNames, null);
        return command;
    }

    public MappedCommand<T> CompileMapped<T>(string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
    {
        if (Find(CommandCache<T>.Mapped, commandText, fromTypeOption, caseSensitiveParamsMatching, null, null) is { } last)
        {
            return last;
        }

        var command = new MappedCommand<T>(contextProvider, commandText, contextProvider.ParamsFromCommandText(commandText),
            FieldSettings.FromTypeShared<T>(fromTypeOption), caseSensitiveParamsMatching);
        CommandCache<T>.Mapped = Keep(command, commandText, fromTypeOption, caseSensitiveParamsMatching, null, null);
        return command;
    }

    public MappedCommand<T> CompileMapped<T>(T proto, string commandText, IList<string>? paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default,
        bool caseSensitiveParamsMatching = false)
    {
        return CompileMapped<T>(commandText, paramNames, fromTypeOption, caseSensitiveParamsMatching);
    }

    public MappedCommand<T> CompileMapped<T>(T proto, string commandText, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
    {
        return CompileMapped<T>(commandText, fromTypeOption, caseSensitiveParamsMatching);
    }

    #region Stored procedures

    public MappedCommand<T> CompileProcedure<T>(string name, IList<string>? paramNames, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
    {
        return new MappedCommand<T>(contextProvider, name, paramNames, settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
    }

    public MappedCommand<T> CompileProcedure<T>(string name, IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching = false)
    {
        return new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, caseSensitiveParamsMatching, CommandType.StoredProcedure);
    }

    public MappedCommand<T> CompileProcedure<T>(string name, IList<string>? paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default,
        bool caseSensitiveParamsMatching = false)
    {
        // no names means no parameters; in the cache a missing list stands for the names of the members
        paramNames ??= Array.Empty<string>();

        if (Find(CommandCache<T>.Procedure, name, fromTypeOption, caseSensitiveParamsMatching, paramNames, null) is { } last)
        {
            return last;
        }

        var command = new MappedCommand<T>(contextProvider, name, paramNames, FieldSettings.FromTypeShared<T>(fromTypeOption), caseSensitiveParamsMatching,
            CommandType.StoredProcedure);
        CommandCache<T>.Procedure = Keep(command, name, fromTypeOption, caseSensitiveParamsMatching, paramNames, null);
        return command;
    }

    public MappedCommand<T> CompileProcedure<T>(string name, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
    {
        if (Find(CommandCache<T>.Procedure, name, fromTypeOption, caseSensitiveParamsMatching, null, null) is { } last)
        {
            return last;
        }

        var settings = FieldSettings.FromTypeShared<T>(fromTypeOption);
        var command = new MappedCommand<T>(contextProvider, name, contextProvider.ParamsFromSettings(settings), settings, caseSensitiveParamsMatching,
            CommandType.StoredProcedure);
        CommandCache<T>.Procedure = Keep(command, name, fromTypeOption, caseSensitiveParamsMatching, null, null);
        return command;
    }

    public MappedCommand<T> CompileProcedure<T>(T proto, string name, IList<string>? paramNames, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
    {
        return CompileProcedure<T>(name, paramNames, fromTypeOption, caseSensitiveParamsMatching);
    }

    public MappedCommand<T> CompileProcedure<T>(T proto, string name, FromTypeOption fromTypeOption = FromTypeOption.Default, bool caseSensitiveParamsMatching = false)
    {
        return CompileProcedure<T>(name, fromTypeOption, caseSensitiveParamsMatching);
    }

    #endregion

    #region Special commands

    #region Insert

    public MappedCommand<T> CompileInsert<T>(string tableName, IList<FieldSettings<T>> settings, params string[] ignoreFields)
    {
        var fields = settings.Select(x => x.Name).Except(ToMemberNames(ignoreFields, settings)).ToArray();
        string query = contextProvider.CommandTextGenerator.Insert(tableName, fields);
        return new MappedCommand<T>(contextProvider, query, fields, settings, true);
    }

    public MappedCommand<T> CompileInsert<T>(string tableName, FromTypeOption fromTypeOption, params string[] ignoreFields)
    {
        ignoreFields ??= [];

        if (Find(CommandCache<T>.Insert, tableName, fromTypeOption, false, ignoreFields, null) is { } last)
        {
            return last;
        }

        var command = CompileInsert(tableName, FieldSettings.FromTypeShared<T>(fromTypeOption), ignoreFields);
        CommandCache<T>.Insert = Keep(command, tableName, fromTypeOption, false, ignoreFields, null);
        return command;
    }

    public MappedCommand<T> CompileInsert<T>(string tableName, params string[] ignoreFields)
    {
        return CompileInsert<T>(tableName, FromTypeOption.Default, ignoreFields);
    }

    public MappedCommand<T> CompileInsert<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] ignoreFields)
    {
        return CompileInsert<T>(tableName, fromTypeOption, ignoreFields);
    }

    public MappedCommand<T> CompileInsert<T>(T proto, string tableName, params string[] ignoreFields)
    {
        return CompileInsert<T>(tableName, FromTypeOption.Default, ignoreFields);
    }

    public MappedCommand<T> CompileInsertWithOutput<T>(string tableName, IList<FieldSettings<T>> settings,
        IList<string>? ignoreFields, params string[] outputFields)
    {
        var ignFields = ignoreFields?.ToArray() ?? [];

        if (outputFields.Length == 0)
        {
            return CompileInsert(tableName, settings, ignFields);
        }

        var fields = settings.Select(x => x.Name).Except(ToMemberNames(ignFields, settings)).ToArray();
        string query = contextProvider.CommandTextGenerator.InsertWithOutput(tableName, fields, outputFields);
        return new MappedCommand<T>(contextProvider, query, fields, settings, true);
    }

    // with the members of the type, as the methods above that take a FromTypeOption
    internal MappedCommand<T> CompileInsertWithOutput<T>(string tableName, FromTypeOption fromTypeOption, IList<string>? ignoreFields, string[] outputFields)
    {
        ignoreFields ??= Array.Empty<string>();

        if (Find(CommandCache<T>.InsertWithOutput, tableName, fromTypeOption, false, ignoreFields, outputFields) is { } last)
        {
            return last;
        }

        var command = CompileInsertWithOutput(tableName, FieldSettings.FromTypeShared<T>(fromTypeOption), ignoreFields, outputFields);
        CommandCache<T>.InsertWithOutput = Keep(command, tableName, fromTypeOption, false, ignoreFields, outputFields);
        return command;
    }

    #endregion

    #region Update

    public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        CheckKeyFields(keyFields);
        var keys = ToMemberNames(keyFields, settings);
        var fields = settings.Select(x => x.Name).Except(ToMemberNames(notKeyIgnoreFields, settings)).ToArray();
        var fieldsToUpdate = fields.Except(keys).ToArray();
        if (fieldsToUpdate.Length == 0)
        {
            throw new ArgumentException("Update has no fields to set: all fields are key fields or ignored", nameof(keyFields));
        }

        string query = contextProvider.CommandTextGenerator.Update(tableName, keys, fieldsToUpdate);
        return new MappedCommand<T>(contextProvider, query, fields, settings, true);
    }

    public MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
    {
        return CompileUpdate(tableName, settings, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        notKeyIgnoreFields ??= Array.Empty<string>();

        if (Find(CommandCache<T>.Update, tableName, fromTypeOption, false, keyFields, notKeyIgnoreFields) is { } last)
        {
            return last;
        }

        var command = CompileUpdate(tableName, FieldSettings.FromTypeShared<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        CommandCache<T>.Update = Keep(command, tableName, fromTypeOption, false, keyFields, notKeyIgnoreFields);
        return command;
    }

    public MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        return CompileUpdate<T>(tableName, fromTypeOption, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileUpdate<T>(string tableName, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileUpdate<T>(tableName, FromTypeOption.Default, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileUpdate<T>(string tableName, params string[] keyFields)
    {
        return CompileUpdate<T>(tableName, FromTypeOption.Default, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileUpdate<T>(tableName, fromTypeOption, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        return CompileUpdate<T>(tableName, fromTypeOption, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileUpdate<T>(tableName, FromTypeOption.Default, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileUpdate<T>(T proto, string tableName, params string[] keyFields)
    {
        return CompileUpdate<T>(tableName, FromTypeOption.Default, keyFields, Array.Empty<string>());
    }

    #endregion

    #region Delete

    public MappedCommand<T> CompileDelete<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
    {
        var keys = keyFields == null || keyFields.Length == 0 ? settings.GetNames() : ToMemberNames(keyFields, settings);
        string query = contextProvider.CommandTextGenerator.Delete(tableName, keys);
        return new MappedCommand<T>(contextProvider, query, keys, settings, true);
    }

    public MappedCommand<T> CompileDelete<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        // no key fields: every member is a key field
        keyFields ??= [];

        if (Find(CommandCache<T>.Delete, tableName, fromTypeOption, false, keyFields, null) is { } last)
        {
            return last;
        }

        var command = CompileDelete(tableName, FieldSettings.FromTypeShared<T>(fromTypeOption), keyFields);
        CommandCache<T>.Delete = Keep(command, tableName, fromTypeOption, false, keyFields, null);
        return command;
    }

    public MappedCommand<T> CompileDelete<T>(string tableName, params string[] keyFields)
    {
        return CompileDelete<T>(tableName, FromTypeOption.Default, keyFields);
    }

    public MappedCommand<T> CompileDelete<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        return CompileDelete<T>(tableName, fromTypeOption, keyFields);
    }

    public MappedCommand<T> CompileDelete<T>(T proto, string tableName, params string[] keyFields)
    {
        return CompileDelete<T>(tableName, FromTypeOption.Default, keyFields);
    }

    #endregion

    #region Merge

    public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        CheckKeyFields(keyFields);
        var keys = ToMemberNames(keyFields, settings);
        var fields = settings.Select(x => x.Name).Except(ToMemberNames(notKeyIgnoreFields, settings)).ToArray();
        var fieldsToUpdate = fields.Except(keys).ToArray(); // none: the text generator makes an insert if missing
        string query = contextProvider.CommandTextGenerator.Merge(tableName, keys, fieldsToUpdate);
        return new MappedCommand<T>(contextProvider, query, fields, settings, true);
    }

    public MappedCommand<T> CompileMerge<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
    {
        return CompileMerge(tableName, settings, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        notKeyIgnoreFields ??= Array.Empty<string>();

        if (Find(CommandCache<T>.Merge, tableName, fromTypeOption, false, keyFields, notKeyIgnoreFields) is { } last)
        {
            return last;
        }

        var command = CompileMerge(tableName, FieldSettings.FromTypeShared<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
        CommandCache<T>.Merge = Keep(command, tableName, fromTypeOption, false, keyFields, notKeyIgnoreFields);
        return command;
    }

    public MappedCommand<T> CompileMerge<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        return CompileMerge<T>(tableName, fromTypeOption, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileMerge<T>(string tableName, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileMerge<T>(tableName, FromTypeOption.Default, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileMerge<T>(string tableName, params string[] keyFields)
    {
        return CompileMerge<T>(tableName, FromTypeOption.Default, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileMerge<T>(tableName, fromTypeOption, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileMerge<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
        return CompileMerge<T>(tableName, fromTypeOption, keyFields, Array.Empty<string>());
    }

    public MappedCommand<T> CompileMerge<T>(T proto, string tableName, IList<string> keyFields, IList<string>? notKeyIgnoreFields)
    {
        return CompileMerge<T>(tableName, FromTypeOption.Default, keyFields, notKeyIgnoreFields);
    }

    public MappedCommand<T> CompileMerge<T>(T proto, string tableName, params string[] keyFields)
    {
        return CompileMerge<T>(tableName, FromTypeOption.Default, keyFields, Array.Empty<string>());
    }

    #endregion

    #endregion

    #endregion

    // the command compiled last for the type and kind, when it was compiled by this provider from the same arguments
    private MappedCommand<T>? Find<T>(CompiledCommand<T>? last, string text, FromTypeOption fromTypeOption, bool caseSensitive,
        IList<string>? names1, IList<string>? names2)
    {
        return last != null && last.IsFor(contextProvider, text, fromTypeOption, caseSensitive, names1, names2) ? last.Command : null;
    }

    private CompiledCommand<T> Keep<T>(MappedCommand<T> command, string text, FromTypeOption fromTypeOption, bool caseSensitive,
        IList<string>? names1, IList<string>? names2)
    {
        return new CompiledCommand<T>(contextProvider, text, fromTypeOption, caseSensitive, names1, names2, command);
    }

    // names the caller gives (keys, ignored fields) match members case-insensitively, as parameters do by default;
    // an exact match wins, and the generated text gets the member's spelling; unknown names are kept as given
    private static string[] ToMemberNames<T>(IEnumerable<string>? names, IList<FieldSettings<T>> settings)
    {
        return (names ?? [])
            .Select(x => settings.Any(y => y.Name == x)
                ? x
                : settings.FirstOrDefault(y => string.Equals(y.Name, x, StringComparison.OrdinalIgnoreCase))?.Name ?? x)
            .ToArray();
    }

    // without key fields the generated where / on clause is empty, which the server rejects
    private static void CheckKeyFields(IList<string> keyFields)
    {
        if (keyFields == null || keyFields.Count == 0)
        {
            throw new ArgumentException("At least one key field is required", nameof(keyFields));
        }
    }
}
