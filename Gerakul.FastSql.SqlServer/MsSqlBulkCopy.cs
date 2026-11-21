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

    // With the Source selector and nothing said about case, the names of the source fields go to SqlBulkCopy as
    // they are, and the columns of the table are not read
    private bool NamesGoAsTheyAre => bulkOptions.FieldsSelector!.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue;

    internal MsSqlBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, SqlBulkOptions bulkOptions, params string[] fields)
        : base(context, reader, destinationTable, bulkOptions, fields)
    {
    }

    protected override void WriteToServer()
    {
        string[] sourceFields = GetSourceFields();

        if (bulkOptions.CreateTable!.Value)
        {
            context.CreateSimple(GetCreateTableScript(sourceFields)).ExecuteNonQuery();
        }

        var map = NamesGoAsTheyAre ? GetMapping(sourceFields) : GetMapping(sourceFields, GetTableColumns());

        if (map.Length > 0)
        {
            using var bcp = CreateSqlBulkCopy(map);
            bcp.WriteToServer(reader);
        }
    }

    protected override async Task WriteToServerAsync(CancellationToken cancellationToken)
    {
        string[] sourceFields = GetSourceFields();

        if (bulkOptions.CreateTable!.Value)
        {
            await context.CreateSimple(GetCreateTableScript(sourceFields)).ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var map = NamesGoAsTheyAre ? GetMapping(sourceFields) : GetMapping(sourceFields, await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false));

        if (map.Length > 0)
        {
            using var bcp = CreateSqlBulkCopy(map);
            await bcp.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
        }
    }

    // the fields the caller named, or all the fields of the source
    private string[] GetSourceFields()
    {
        return fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();
    }

    private static Mapping[] GetMapping(string[] sourceFields)
    {
        return sourceFields.Select(x => new Mapping(x, x)).ToArray();
    }

    // the source fields that have a column in the table, with the column's own spelling; checked against the FieldsSelector
    private Mapping[] GetMapping(string[] sourceFields, string[] tableColumns)
    {
        var comparer = bulkOptions.CaseSensitiveFieldsMatching == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var map = sourceFields.Join(tableColumns, x => x, x => x, (x, y) => new Mapping(x, y), comparer).ToArray();

        CheckFieldSelection(bulkOptions.FieldsSelector!.Value, sourceFields, tableColumns, comparer);

        return map;
    }

    // the caller disposes it; when the setup fails it is disposed here
    private SqlBulkCopy CreateSqlBulkCopy(Mapping[] map)
    {
        var bcp = new SqlBulkCopy(Connection, SqlBulkOptions.SqlBulkCopyOptions!.Value, Transaction);

        try
        {
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

            return bcp;
        }
        catch
        {
            ((IDisposable)bcp).Dispose();
            throw;
        }
    }

    private string GetCreateTableScript(string[] sourceFields)
    {
        var colDefs = reader.GetColumnDefinitions(SqlBulkOptions.ColumnDefinitionOptions, bulkOptions.IgnoreDataReaderSchemaTable!.Value)
            .Where(x => x != null && sourceFields.Contains(x.Name)).ToArray(); // null: a type without a column type

        if (colDefs.Length != sourceFields.Length)
        {
            throw CreateTableFields.Problems(destinationTable, sourceFields, colDefs.Select(x => x!.Name), reader);
        }

        return colDefs.CreateTableScript(destinationTable, bulkOptions.CheckTableIfNotExistsBeforeCreation!.Value);
    }

    private string[] GetTableColumns()
    {
        string[] columns = [];
        context.CreateSimple($"select top 0 * from {destinationTable} with(nolock)")
            .UseReader(r => columns = r.GetColumnNames().ToArray());

        return columns;
    }

    private async Task<string[]> GetTableColumnsAsync(CancellationToken cancellationToken)
    {
        string[] columns = [];
        await context.CreateSimple($"select top 0 * from {destinationTable} with(nolock)")
            .UseReaderAsync(cancellationToken, r =>
            {
                columns = r.GetColumnNames().ToArray();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        return columns;
    }
}
