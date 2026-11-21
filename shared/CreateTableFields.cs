using Gerakul.FastSql.Common;
using System.Data.Common;

namespace Gerakul.FastSql;

// Compiled into both provider assemblies (each project links this file), so that their table creation gives the
// same messages without public API in Common or a dependency of one package on the internals of another
internal static class CreateTableFields
{
    // The exception for a field list that the column definitions do not cover: a field that the source does not
    // have, one whose type has no column type, or a field listed twice. The list is compared with the names of the
    // source as they are, case included (Q130)
    public static InvalidOperationException Problems(string destinationTable, string[] sourceFields, IEnumerable<string> definedFields, DbDataReader reader)
    {
        var readerFields = reader.GetColumnNames().ToArray();
        var duplicates = sourceFields.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => $"'{x.Key}'").ToArray();
        var missing = sourceFields.Except(definedFields).ToArray();
        var unknown = missing.Except(readerFields).Select(x => DescribeUnknown(x, readerFields)).ToArray();
        var untyped = missing.Intersect(readerFields).Select(x => $"'{x}' ({reader.GetFieldType(reader.GetOrdinal(x)).Name})").ToArray();
        var problems = new List<string>(3);

        if (unknown.Length > 0)
        {
            problems.Add($"the source has no {(unknown.Length == 1 ? "field" : "fields")} {string.Join(", ", unknown)}");
        }

        if (untyped.Length > 0)
        {
            problems.Add($"there is no column type for the {(untyped.Length == 1 ? "field" : "fields")} {string.Join(", ", untyped)}");
        }

        if (duplicates.Length > 0)
        {
            problems.Add($"the field list names {string.Join(", ", duplicates)} more than once");
        }

        if (problems.Count == 0)
        {
            problems.Add("the field list does not match the fields of the source");
        }

        return new InvalidOperationException($"Cannot create table {destinationTable}: {string.Join("; ", problems)}");
    }

    // a name that differs from a field of the source only in case says so
    private static string DescribeUnknown(string field, string[] readerFields)
    {
        var sameIgnoringCase = readerFields.FirstOrDefault(x => string.Equals(x, field, StringComparison.OrdinalIgnoreCase));

        return sameIgnoringCase == null ? $"'{field}'" : $"'{field}' (it has '{sameIgnoringCase}': the field list is compared with case)";
    }
}
