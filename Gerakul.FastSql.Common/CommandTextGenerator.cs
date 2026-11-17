namespace Gerakul.FastSql;

/// <summary>
/// Generates the text of commands in the dialect of a provider.
/// </summary>
public abstract class CommandTextGenerator
{
    /// <summary>
    /// The names of the columns, separated by commas, written the way the provider writes them.
    /// </summary>
    public abstract string ColumnList(params string[] fields);

    /// <summary>
    /// The parameters of the fields, separated by commas.
    /// </summary>
    public abstract string ParamList(params string[] fields);

    /// <summary>
    /// The conditions <c>column = parameter</c> of the fields, joined with <c>and</c>.
    /// </summary>
    public abstract string WhereClause(params string[] fields);

    /// <summary>
    /// The assignments <c>column = parameter</c> of the fields, separated by commas.
    /// </summary>
    public abstract string SetClause(params string[] fields);

    /// <summary>
    /// An insert of the fields into the table.
    /// </summary>
    public abstract string Insert(string tableName, params string[] fields);

    /// <summary>
    /// An insert of the fields that returns the output fields of the inserted row.
    /// </summary>
    public abstract string InsertWithOutput(string tableName, string[] fields, params string[] outputFields);

    /// <summary>
    /// An update of the fields in the rows that the where clause, or the key fields, find.
    /// </summary>
    public abstract string Update(string tableName, string whereClause, params string[] fields);

    /// <summary>
    /// An update of the fields in the rows that the where clause, or the key fields, find.
    /// </summary>
    public string Update(string tableName, IEnumerable<string> keyFields, params string[] fields)
    {
        return Update(tableName, WhereClause(keyFields.ToArray()), fields);
    }

    /// <summary>
    /// A delete of the rows that the where clause, or the key fields, find.
    /// </summary>
    public abstract string Delete(string tableName, string whereClause);

    /// <summary>
    /// A delete of the rows that the where clause, or the key fields, find.
    /// </summary>
    public string Delete(string tableName, params string[] keyFields)
    {
        return Delete(tableName, WhereClause(keyFields));
    }

    /// <summary>
    /// An insert or update of the fields by the key fields.
    /// </summary>
    public abstract string Merge(string tableName, IEnumerable<string> keyFields, params string[] fields);
}
