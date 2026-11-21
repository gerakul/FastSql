namespace Gerakul.FastSql.PostgreSQL;

/// <summary>
/// Generates the text of commands for PostgreSQL.
/// </summary>
public class NpgsqlCommandTextGenerator : CommandTextGenerator
{
    // a double quote inside a name is written twice
    private static string Quote(string name)
    {
        return $"\"{name.Replace("\"", "\"\"")}\"";
    }

    /// <inheritdoc />
    public override string ColumnList(params string[] fields)
    {
        return string.Join(", ", fields.Select(x => Quote(x)));
    }

    /// <inheritdoc />
    public override string ParamList(params string[] fields)
    {
        return string.Join(", ", fields.Select(x => $"@{x}"));
    }

    /// <inheritdoc />
    public override string WhereClause(params string[] fields)
    {
        return string.Join(" and ", fields.Select(x => $"{Quote(x)} = @{x}"));
    }

    /// <inheritdoc />
    public override string SetClause(params string[] fields)
    {
        return string.Join(", ", fields.Select(x => $"{Quote(x)} = @{x}"));
    }

    /// <inheritdoc />
    public override string Insert(string tableName, params string[] fields)
    {
        return $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)});";
    }

    /// <inheritdoc />
    public override string InsertWithOutput(string tableName, string[] fields, params string[] outputFields)
    {
        return $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)}) returning {ColumnList(outputFields)};";
    }

    /// <inheritdoc />
    /// <remarks>
    /// The value is the inserted row's own value of its <c>serial</c> or identity column (the first one), which
    /// the catalog lookup finds, as a <c>bigint</c>; null when the table has no such column.
    /// </remarks>
    public override string InsertAndGetID(string tableName, params string[] fields)
    {
        var literal = tableName.Replace("'", "''");

        // the left join gives a row with null for a table without such a column, as scope_identity() does
        return $"with inserted as (insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)}) returning *) "
            + "select (to_jsonb(inserted) ->> c.attname)::bigint from inserted left join "
            + $"(select attname from pg_attribute where attrelid = '{literal}'::regclass and attnum > 0 and not attisdropped "
            + $"and pg_get_serial_sequence('{literal}', attname) is not null order by attnum limit 1) c on true;";
    }

    /// <inheritdoc />
    public override string Update(string tableName, string whereClause, params string[] fields)
    {
        return $"update {tableName} set {SetClause(fields)} where {whereClause};";
    }

    /// <inheritdoc />
    public override string Delete(string tableName, string whereClause)
    {
        return $"delete from {tableName} where {whereClause};";
    }

    /// <inheritdoc />
    public override string Merge(string tableName, IEnumerable<string> keyFields, params string[] fields)
    {
        var allFields = keyFields.Union(fields).ToArray();
        var setClause = string.Join(", ", fields.Select(x => $"{Quote(x)} = excluded.{Quote(x)}"));

        // with nothing to update an existing row stays as it is: insert if missing
        var conflictAction = fields.Length == 0 ? "do nothing;" : $"do update set {setClause};";

        var cmd = $"insert into {tableName} ({ColumnList(allFields)}) "
            + Environment.NewLine + $"values ({ParamList(allFields)}) "
            + Environment.NewLine + $"on conflict ({ColumnList(keyFields.ToArray())}) "
            + Environment.NewLine + conflictAction;

        return cmd;
    }
}
