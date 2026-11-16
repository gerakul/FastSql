using Gerakul.FastSql.Common;
using System.Data.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL;

internal sealed class NpgsqlBulkCopy : BulkCopy
{
    private readonly record struct Mapping(string Source, string Destination);

    private NpgsqlConnection? Connection => context switch
    {
        TransactionContext transactionContext => (NpgsqlConnection?)transactionContext.Transaction.Connection,
        ConnectionContext connectionContext => (NpgsqlConnection)connectionContext.Connection,
        _ => null
    };

    // With the Source selector and nothing said about case, the names of the source fields are the names of the
    // columns, and the columns of the table are not read
    private bool NamesGoAsTheyAre => bulkOptions.FieldsSelector!.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue;

    internal NpgsqlBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, NpgsqlBulkOptions bulkOptions, params string[] fields)
        : base(context, reader, destinationTable, bulkOptions, fields)
    {
    }

    protected override void WriteToServer()
    {
        CheckOptions();

        string[] sourceFields = GetSourceFields();
        var map = NamesGoAsTheyAre ? GetMapping(sourceFields) : GetMapping(sourceFields, GetTableColumns());

        if (map.Length > 0)
        {
            var ordinals = GetOrdinals(map);
            using var writer = Connection!.BeginBinaryImport(GetCopyCommand(map));

            while (reader.Read())
            {
                writer.StartRow();

                foreach (var ordinal in ordinals)
                {
                    writer.Write(reader.GetValue(ordinal));
                }
            }

            writer.Complete();
        }
    }

    protected override async Task WriteToServerAsync(CancellationToken cancellationToken)
    {
        CheckOptions();

        string[] sourceFields = GetSourceFields();
        var map = NamesGoAsTheyAre ? GetMapping(sourceFields) : GetMapping(sourceFields, await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false));

        if (map.Length > 0)
        {
            var ordinals = GetOrdinals(map);
            var writer = await Connection!.BeginBinaryImportAsync(GetCopyCommand(map), cancellationToken).ConfigureAwait(false);
            await using var writerScope = writer.ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);

                foreach (var ordinal in ordinals)
                {
                    await writer.WriteAsync(reader.GetValue(ordinal), cancellationToken).ConfigureAwait(false);
                }
            }

            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // the fields the caller named, or all the fields of the source
    private string[] GetSourceFields()
    {
        return fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();
    }

    private void CheckOptions()
    {
        if (bulkOptions.CreateTable!.Value)
        {
            throw new NotImplementedException("Table creation is not implemented for PostgreSQL");
        }
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

    private int[] GetOrdinals(Mapping[] map)
    {
        return map.Select(x => reader.GetOrdinal(x.Source)).ToArray();
    }

    private string GetCopyCommand(Mapping[] map)
    {
        var columns = context.ContextProvider.CommandTextGenerator.ColumnList(map.Select(x => x.Destination).ToArray());
        return $"COPY {destinationTable} ({columns}) FROM STDIN (FORMAT BINARY)";
    }

    private string[] GetTableColumns()
    {
        string[] columns = [];
        context.CreateSimple($"select * from {destinationTable} limit 0")
            .UseReader(r => columns = r.GetColumnNames().ToArray());

        return columns;
    }

    private async Task<string[]> GetTableColumnsAsync(CancellationToken cancellationToken)
    {
        string[] columns = [];
        await context.CreateSimple($"select * from {destinationTable} limit 0")
            .UseReaderAsync(cancellationToken, r =>
            {
                columns = r.GetColumnNames().ToArray();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        return columns;
    }
}
