using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// The base of the bulk copy of a provider: writes the rows of a data reader to a table.
/// <c>ContextProvider.GetBulkCopy</c> creates it; the <c>WriteToServer</c> extension methods use it.
/// </summary>
public abstract class BulkCopy
{
    /// <summary>
    /// The context the copy runs in.
    /// </summary>
    protected ScopedContext context;

    /// <summary>
    /// The options of the copy, with every value set.
    /// </summary>
    protected BulkOptions bulkOptions;

    /// <summary>
    /// The name of the table the rows go to.
    /// </summary>
    protected string destinationTable;

    /// <summary>
    /// The source of the rows.
    /// </summary>
    protected DbDataReader reader;

    /// <summary>
    /// The fields of the source to copy; empty for all of them.
    /// </summary>
    protected string[] fields;

    /// <summary>
    /// Keeps the arguments in the fields of the same names.
    /// </summary>
    protected BulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields)
    {
        this.context = context;
        this.bulkOptions = bulkOptions;
        this.destinationTable = destinationTable;
        this.reader = reader;
        this.fields = fields;
    }

    /// <summary>
    /// Writes the rows of the reader to the destination table.
    /// </summary>
    protected internal abstract void WriteToServer();

    /// <summary>
    /// Writes the rows of the reader to the destination table.
    /// </summary>
    protected internal abstract Task WriteToServerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the numbers of source fields, of destination columns and of the names they share against the
    /// selector; an <c>InvalidOperationException</c> when the selector is not satisfied.
    /// </summary>
    protected void CheckFieldSelection(FieldsSelector fieldSelector, int sourceNum, int destinationNum, int commonNum)
    {
        Helpers.CheckFieldSelection(fieldSelector, sourceNum, destinationNum, commonNum);
    }

    // The same check, with an exception that names the fields of the source and the columns of the destination
    // table that have no counterpart. The comparer is the one the fields are mapped to the columns with
    /// <summary>
    /// Checks the source fields and the destination columns against the selector. The
    /// <c>InvalidOperationException</c> names the fields and the columns that have no counterpart. The comparer is
    /// the one the fields are matched to the columns with.
    /// </summary>
    protected void CheckFieldSelection(FieldsSelector fieldSelector, IReadOnlyCollection<string> sourceFields,
        IReadOnlyCollection<string> destinationFields, StringComparer comparer)
    {
        var fieldsWithoutColumn = sourceFields.Except(destinationFields, comparer).ToArray();
        var columnsWithoutField = destinationFields.Except(sourceFields, comparer).ToArray();

        bool satisfied = fieldSelector switch
        {
            FieldsSelector.Source => fieldsWithoutColumn.Length == 0,
            FieldsSelector.Destination => columnsWithoutField.Length == 0,
            FieldsSelector.Common => true,
            FieldsSelector.Both => fieldsWithoutColumn.Length == 0 && columnsWithoutField.Length == 0,
            _ => throw new ArgumentException($"Unknown FieldsSelector. Value: {fieldSelector}")
        };

        if (!satisfied)
        {
            throw new InvalidOperationException(Messages.BulkFieldSelection(fieldSelector, destinationTable, fieldsWithoutColumn, columnsWithoutField));
        }
    }
}
