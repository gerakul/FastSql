namespace Gerakul.FastSql.Common;

// The last command that was compiled for a type, per kind of command. context.Insert("Employee", value) asks for
// the same command on every call; with this it is compiled once. There is one entry per type and kind, so the
// cache cannot grow, whatever table names and command texts an application passes
internal static class CommandCache<T>
{
    public static CompiledCommand<T>? Insert;
    public static CompiledCommand<T>? InsertWithOutput;
    public static CompiledCommand<T>? InsertAndGetID;
    public static CompiledCommand<T>? Update;
    public static CompiledCommand<T>? Delete;
    public static CompiledCommand<T>? Merge;
    public static CompiledCommand<T>? Mapped;
    public static CompiledCommand<T>? Procedure;
}

// a command with what it was compiled from; immutable, so threads share it without a lock
internal sealed class CompiledCommand<T>
{
    private readonly ContextProvider contextProvider;
    private readonly string text;
    private readonly FromTypeOption fromTypeOption;
    private readonly bool caseSensitive;
    private readonly string[]? names1;
    private readonly string[]? names2;

    public MappedCommand<T> Command { get; }

    // text: a table name, a command text or a procedure name; the name lists are copied, the caller may change its own
    public CompiledCommand(ContextProvider contextProvider, string text, FromTypeOption fromTypeOption, bool caseSensitive,
        IList<string>? names1, IList<string>? names2, MappedCommand<T> command)
    {
        this.contextProvider = contextProvider;
        this.text = text;
        this.fromTypeOption = fromTypeOption;
        this.caseSensitive = caseSensitive;
        this.names1 = names1?.ToArray();
        this.names2 = names2?.ToArray();
        this.Command = command;
    }

    public bool IsFor(ContextProvider contextProvider, string text, FromTypeOption fromTypeOption, bool caseSensitive,
        IList<string>? names1, IList<string>? names2)
    {
        return ReferenceEquals(this.contextProvider, contextProvider)
            && this.text == text
            && this.fromTypeOption == fromTypeOption
            && this.caseSensitive == caseSensitive
            && Same(this.names1, names1)
            && Same(this.names2, names2);
    }

    // the same names in the same order; a missing list is not an empty one
    private static bool Same(string[]? kept, IList<string>? given)
    {
        if (kept == null || given == null)
        {
            return kept == null && given == null;
        }

        if (kept.Length != given.Count)
        {
            return false;
        }

        for (int i = 0; i < kept.Length; i++)
        {
            if (kept[i] != given[i])
            {
                return false;
            }
        }

        return true;
    }
}
