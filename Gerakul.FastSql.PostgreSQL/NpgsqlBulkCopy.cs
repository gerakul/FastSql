using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Npgsql;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.PostgreSQL
{
    internal sealed class NpgsqlBulkCopy : BulkCopy
    {
        private readonly record struct Mapping(string Source, string Destination);

        private NpgsqlConnection Connection => context switch
        {
            TransactionContext transactionContext => (NpgsqlConnection)transactionContext.Transaction.Connection,
            ConnectionContext connectionContext => (NpgsqlConnection)connectionContext.Connection,
            _ => null
        };

        private NpgsqlTransaction Transaction => context is TransactionContext transactionContext ? (NpgsqlTransaction)transactionContext.Transaction : null;

        private NpgsqlBulkOptions NpgsqlBulkOptions => (NpgsqlBulkOptions)bulkOptions;

        internal NpgsqlBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, NpgsqlBulkOptions bulkOptions, params string[] fields)
            : base(context, reader, destinationTable, bulkOptions, fields)
        {
        }

        protected override void WriteToServer()
        {
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

            if (bulkOptions.CreateTable.Value)
            {
                throw new NotImplementedException("Table creation is not implemented for PostgreSQL");
                //CreateTable(sourceFields);
            }

            Mapping[] map;

            if (bulkOptions.FieldsSelector.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue)
            {
                map = sourceFields.Select(x => new Mapping(x, x)).ToArray();
            }
            else
            {
                string[] destFields = GetTableColumns().ToArray();

                var comparer = bulkOptions.CaseSensitiveFieldsMatching == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
                map = sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping(x, y), comparer).ToArray();

                CheckFieldSelection(bulkOptions.FieldsSelector.Value, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                var ordinals = map.Select(x => reader.GetOrdinal(x.Source)).ToArray();
                var columns = context.ContextProvider.CommandTextGenerator.ColumnList(map.Select(x => x.Destination).ToArray());
                using var writer = Connection.BeginBinaryImport($"COPY {destinationTable} ({columns}) FROM STDIN (FORMAT BINARY)");

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
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

            if (bulkOptions.CreateTable.Value)
            {
                throw new NotImplementedException("Table creation is not implemented for PostgreSQL");
                //CreateTable(sourceFields);
            }

            Mapping[] map;

            if (bulkOptions.FieldsSelector.Value == FieldsSelector.Source && !bulkOptions.CaseSensitiveFieldsMatching.HasValue)
            {
                map = sourceFields.Select(x => new Mapping(x, x)).ToArray();
            }
            else
            {
                string[] destFields = (await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

                var comparer = bulkOptions.CaseSensitiveFieldsMatching == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
                map = sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping(x, y), comparer).ToArray();

                CheckFieldSelection(bulkOptions.FieldsSelector.Value, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                var ordinals = map.Select(x => reader.GetOrdinal(x.Source)).ToArray();
                var columns = context.ContextProvider.CommandTextGenerator.ColumnList(map.Select(x => x.Destination).ToArray());
                var writer = await Connection.BeginBinaryImportAsync($"COPY {destinationTable} ({columns}) FROM STDIN (FORMAT BINARY)", cancellationToken).ConfigureAwait(false);
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

        private IList<string> GetTableColumns()
        {
            var result = new List<string>();
            context.CreateSimple($"select * from {destinationTable} limit 0")
                .UseReader(r => result.AddRange(r.GetColumnNames()));

            return result;
        }

        private async Task<IList<string>> GetTableColumnsAsync(CancellationToken cancellationToken)
        {
            var result = new List<string>();
            await context.CreateSimple($"select * from {destinationTable} limit 0")
                .UseReaderAsync(cancellationToken, r =>
                {
                    result.AddRange(r.GetColumnNames());
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

            return result;
        }
    }
}
