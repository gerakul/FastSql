namespace Gerakul.FastSql.Common;

// The texts of exceptions that name what does not match: fields, members, parameters. Built only when something
// has failed
internal static class Messages
{
    // 'A', 'B'
    public static string Names(IEnumerable<string?> names)
    {
        return string.Join(", ", names.Select(x => $"'{x}'"));
    }

    // "member 'A'" or "members 'A', 'B'"
    public static string Named(string what, IReadOnlyCollection<string?> names)
    {
        return $"{(names.Count == 1 ? what : what + "s")} {Names(names)}";
    }

    public static string TypeName(Type type)
    {
        // the compiler's name of an anonymous type says nothing
        return type.Name.StartsWith("<>", StringComparison.Ordinal) ? "the anonymous type" : type.Name;
    }

    public static string DuplicateReaderField(string? name, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "The reader has more than one field without a name: give the columns names (aliases)";
        }

        return $"The reader has more than one field named '{name}'{ComparedIgnoringCase(caseSensitive)}";
    }

    // what: "members" or "constructor parameters"
    public static string DuplicateMembers(string what, string? first, string? second, Type type, bool caseSensitive)
    {
        return caseSensitive
            ? $"The {what} of {TypeName(type)} have the name '{first}' more than once"
            : $"The {what} '{first}' and '{second}' of {TypeName(type)} differ only by case{ComparedIgnoringCase(caseSensitive)}";
    }

    public static string ComparedIgnoringCase(bool caseSensitive)
    {
        return caseSensitive ? "" : " (names are compared case-insensitively)";
    }

    // A FieldsSelector that is not satisfied. what: "member" or "constructor parameter"
    public static string FieldSelection(FieldsSelector fieldsSelector, string what, Type type,
        IReadOnlyCollection<string?> fieldsWithoutMember, IReadOnlyCollection<string?> membersWithoutField)
    {
        var problems = new List<string>(2);

        if (membersWithoutField.Count > 0 && fieldsSelector != FieldsSelector.Source)
        {
            problems.Add($"the reader has no field for the {Named(what, membersWithoutField)} of {TypeName(type)}");
        }

        if (fieldsWithoutMember.Count > 0 && fieldsSelector != FieldsSelector.Destination)
        {
            problems.Add($"{TypeName(type)} has no {what} for the reader {Named("field", fieldsWithoutMember)}");
        }

        return $"FieldsSelector.{fieldsSelector}: {string.Join("; ", problems)}";
    }

    // the same for a bulk copy: the fields of the source and the columns of the destination table
    public static string BulkFieldSelection(FieldsSelector fieldsSelector, string destinationTable,
        IReadOnlyCollection<string?> fieldsWithoutColumn, IReadOnlyCollection<string?> columnsWithoutField)
    {
        var problems = new List<string>(2);

        if (fieldsWithoutColumn.Count > 0 && fieldsSelector != FieldsSelector.Destination)
        {
            problems.Add($"table {destinationTable} has no column for the source {Named("field", fieldsWithoutColumn)}");
        }

        if (columnsWithoutField.Count > 0 && fieldsSelector != FieldsSelector.Source)
        {
            problems.Add($"the source has no field for the {Named("column", columnsWithoutField)} of table {destinationTable}");
        }

        return $"FieldsSelector.{fieldsSelector}: {string.Join("; ", problems)}";
    }

    public const string OtherProvider = "The command was compiled by another context provider than the one of this context. "
        + "A command can be used only with contexts of the provider that compiled it";
}
