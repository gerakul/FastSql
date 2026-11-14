using System.Data.Common;

namespace Gerakul.FastSql.Common;

public abstract class BulkCopy
{
    protected ScopedContext context;

    protected BulkOptions bulkOptions;
    protected string destinationTable;
    protected DbDataReader reader;
    protected string[] fields;

    protected BulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields)
    {
        this.context = context;
        this.bulkOptions = bulkOptions;
        this.destinationTable = destinationTable;
        this.reader = reader;
        this.fields = fields;
    }

    protected internal abstract void WriteToServer();

    protected internal abstract Task WriteToServerAsync(CancellationToken cancellationToken = default);

    protected void CheckFieldSelection(FieldsSelector fieldSelector, int sourceNum, int destinationNum, int commonNum)
    {
        Helpers.CheckFieldSelection(fieldSelector, sourceNum, destinationNum, commonNum);
    }

    // The same check, with an exception that names the fields of the source and the columns of the destination
    // table that have no counterpart. The comparer is the one the fields are mapped to the columns with
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
