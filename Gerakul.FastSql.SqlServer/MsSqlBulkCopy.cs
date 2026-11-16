using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Gerakul.FastSql.SqlServer;

internal sealed class MsSqlBulkCopy : BulkCopy
{
    private readonly record struct Mapping(string Source, string Destination);

    private SqlConnection? Connection => context switch
    {
        TransactionContext transactionContext => (SqlConnection?)transactionContext.Transaction.Connection,
        ConnectionContext connectionContext => (SqlConnection)connectionContext.Connection,
        _ => null
    };

    private SqlTransaction? Transaction => context is TransactionContext transactionContext ? (SqlTransaction)transactionContext.Transaction : null;

    private SqlBulkOptions SqlBulkOptions => (SqlBulkOptions)bulkOptions;

    internal MsSqlBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, SqlBulkOptions bulkOptions, params string[] fields)
        : base(context, reader, destinationTable, bulkOptions, fields)
    {
    }

    protected override void WriteToServer()
    {
        string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

        if (bulkOptions.CreateTable!.Value)
        {
            CreateTable(sourceFields);
        }

        Mapping[] map;

        if (bulkOptions.FieldsSelector!.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue)
        {
            map = sourceFields.Select(x => new Mapping(x, x)).ToArray();
        }
        else
        {
            string[] destFields = GetTableColumns().ToArray();

            var comparer = bulkOptions.CaseSensitiveFieldsMatching == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            map = sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping(x, y), comparer).ToArray();

            CheckFieldSelection(bulkOptions.FieldsSelector!.Value, sourceFields, destFields, comparer);
        }

        if (map.Length > 0)
        {
            using var bcp = new SqlBulkCopy(Connection, SqlBulkOptions.SqlBulkCopyOptions!.Value, Transaction);

            bcp.DestinationTableName = destinationTable;
            if (SqlBulkOptions.BatchSize.HasValue)
            {
                bcp.BatchSize = SqlBulkOptions.BatchSize.Value;
            }

            if (SqlBulkOptions.BulkCopyTimeout.HasValue)
            {
                bcp.BulkCopyTimeout = SqlBulkOptions.BulkCopyTimeout.Value;
            }

            if (SqlBulkOptions.EnableStreaming.HasValue)
            {
                bcp.EnableStreaming = SqlBulkOptions.EnableStreaming.Value;
            }

            foreach (var item in map)
            {
                bcp.ColumnMappings.Add(item.Source, item.Destination);
            }

            bcp.WriteToServer(reader);
        }
    }

    protected override async Task WriteToServerAsync(CancellationToken cancellationToken)
    {
        string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

        if (bulkOptions.CreateTable!.Value)
        {
            await CreateTableAsync(sourceFields, cancellationToken).ConfigureAwait(false);
        }

        Mapping[] map;

        if (bulkOptions.FieldsSelector!.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue)
        {
            map = sourceFields.Select(x => new Mapping(x, x)).ToArray();
        }
        else
        {
            string[] destFields = (await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

            var comparer = bulkOptions.CaseSensitiveFieldsMatching == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            map = sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping(x, y), comparer).ToArray();

            CheckFieldSelection(bulkOptions.FieldsSelector!.Value, sourceFields, destFields, comparer);
        }

        if (map.Length > 0)
        {
            using var bcp = new SqlBulkCopy(Connection, SqlBulkOptions.SqlBulkCopyOptions!.Value, Transaction);

            bcp.DestinationTableName = destinationTable;
            if (SqlBulkOptions.BatchSize.HasValue)
            {
                bcp.BatchSize = SqlBulkOptions.BatchSize.Value;
            }

            if (SqlBulkOptions.BulkCopyTimeout.HasValue)
            {
                bcp.BulkCopyTimeout = SqlBulkOptions.BulkCopyTimeout.Value;
            }

            if (SqlBulkOptions.EnableStreaming.HasValue)
            {
                bcp.EnableStreaming = SqlBulkOptions.EnableStreaming.Value;
            }

            foreach (var item in map)
            {
                bcp.ColumnMappings.Add(item.Source, item.Destination);
            }

            await bcp.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
        }
    }

    private string GetCreateTableScript(string[] sourceFields)
    {
        var colDefs = reader.GetColumnDefinitions(SqlBulkOptions.ColumnDefinitionOptions, bulkOptions.IgnoreDataReaderSchemaTable!.Value)
            .Where(x => x != null && sourceFields.Contains(x.Name)).ToArray(); // null: a type without a column type

        if (colDefs.Length != sourceFields.Length)
        {
            // A field of the list that the source does not have, one whose type has no column type, or a field
            // listed twice. The list is compared with the names of the source as they are, case included (Q130)
            var readerFields = reader.GetColumnNames().ToArray();
            var duplicates = sourceFields.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => $"'{x.Key}'").ToArray();
            var missing = sourceFields.Except(colDefs.Select(x => x!.Name)).ToArray();
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

            throw new InvalidOperationException($"Cannot create table {destinationTable}: {string.Join("; ", problems)}");
        }

        return colDefs.CreateTableScript(destinationTable, bulkOptions.CheckTableIfNotExistsBeforeCreation!.Value);
    }

    // a name that differs from a field of the source only in case says so
    private static string DescribeUnknown(string field, string[] readerFields)
    {
        var sameIgnoringCase = readerFields.FirstOrDefault(x => string.Equals(x, field, StringComparison.OrdinalIgnoreCase));

        return sameIgnoringCase == null ? $"'{field}'" : $"'{field}' (it has '{sameIgnoringCase}': the field list is compared with case)";
    }

    private void CreateTable(string[] sourceFields)
    {
        string cmd = GetCreateTableScript(sourceFields);
        context.CreateSimple(cmd).ExecuteNonQuery();
    }

    private async Task CreateTableAsync(string[] sourceFields, CancellationToken cancellationToken)
    {
        string cmd = GetCreateTableScript(sourceFields);
        await context.CreateSimple(cmd).ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private IList<string> GetTableColumns()
    {
        var result = new List<string>();
        context.CreateSimple($"select top 0 * from {destinationTable} with(nolock)")
            .UseReader(r => result.AddRange(r.GetColumnNames()));

        return result;
    }

    private async Task<IList<string>> GetTableColumnsAsync(CancellationToken cancellationToken)
    {
        var result = new List<string>();
        await context.CreateSimple($"select top 0 * from {destinationTable} with(nolock)")
            .UseReaderAsync(cancellationToken, r =>
            {
                result.AddRange(r.GetColumnNames());
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        return result;
    }
}
