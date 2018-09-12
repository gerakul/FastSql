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
    internal class NpgsqlBulkCopy : BulkCopy
    {
        private class Mapping
        {
            public int Ordinal;
            public string Source;
            public string Destination;
        }

        private NpgsqlConnection Connection
        {
            get
            {
                if (context is TransactionContext)
                {
                    return (NpgsqlConnection)((TransactionContext)context).Transaction.Connection;
                }
                else if (context is ConnectionContext)
                {
                    return (NpgsqlConnection)((ConnectionContext)context).Connection;
                }
                else
                {
                    return null;
                }
            }
        }

        private NpgsqlTransaction Transaction
        {
            get
            {
                if (context is TransactionContext)
                {
                    return (NpgsqlTransaction)((TransactionContext)context).Transaction;
                }
                else
                {
                    return null;
                }
            }
        }

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
                map = sourceFields.Select(x => new Mapping() { Source = x, Destination = x }).ToArray();
            }
            else
            {
                string[] destFields = GetTableColumns().ToArray();

                map = bulkOptions.CaseSensitiveFieldsMatching.HasValue && bulkOptions.CaseSensitiveFieldsMatching.Value ?
                  sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping() { Source = x, Destination = y }).ToArray() :
                  sourceFields.Join(destFields, x => x.ToLowerInvariant(), x => x.ToLowerInvariant(), (x, y) => new Mapping() { Source = x, Destination = y }).ToArray();

                CheckFieldSelection(bulkOptions.FieldsSelector.Value, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                foreach (var item in map)
                {
                    item.Ordinal = reader.GetOrdinal(item.Source);
                }

                var columns = context.ContextProvider.CommandTextGenerator.ColumnList(map.Select(x => x.Destination).ToArray());
                using (var writer = Connection.BeginBinaryImport($"COPY \"{destinationTable}\" ({columns}) FROM STDIN (FORMAT BINARY)"))
                {
                    while (reader.Read())
                    {
                        writer.StartRow();

                        foreach (var item in map)
                        {
                            writer.Write(reader.GetValue(item.Ordinal));
                        }
                    }

                    writer.Complete();
                }
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
                map = sourceFields.Select(x => new Mapping() { Source = x, Destination = x }).ToArray();
            }
            else
            {
                string[] destFields = (await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

                map = bulkOptions.CaseSensitiveFieldsMatching.HasValue && bulkOptions.CaseSensitiveFieldsMatching.Value ?
                  sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping() { Source = x, Destination = y }).ToArray() :
                  sourceFields.Join(destFields, x => x.ToLowerInvariant(), x => x.ToLowerInvariant(), (x, y) => new Mapping() { Source = x, Destination = y }).ToArray();

                CheckFieldSelection(bulkOptions.FieldsSelector.Value, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                foreach (var item in map)
                {
                    item.Ordinal = reader.GetOrdinal(item.Source);
                }

                var columns = context.ContextProvider.CommandTextGenerator.ColumnList(map.Select(x => x.Destination).ToArray());
                using (var writer = Connection.BeginBinaryImport($"COPY \"{destinationTable}\" ({columns}) FROM STDIN (FORMAT BINARY)"))
                {
                    while (reader.Read())
                    {
                        writer.StartRow();

                        foreach (var item in map)
                        {
                            writer.Write(reader.GetValue(item.Ordinal));
                        }
                    }

                    writer.Complete();
                }
            }
        }

        private IList<string> GetTableColumns()
        {
            List<string> result = new List<string>();
            context.CreateSimple(string.Format("select * from \"{0}\" limit 0", destinationTable))
                .UseReader(r =>
                {
                    foreach (var item in r.GetColumnNames())
                    {
                        result.Add(item);
                    }
                });

            return result;
        }

        private async Task<IList<string>> GetTableColumnsAsync(CancellationToken cancellationToken)
        {
            List<string> result = new List<string>();
            await context.CreateSimple(string.Format("select * from \"{0}\" limit 0", destinationTable))
                .UseReaderAsync(r =>
                {
                    foreach (var item in r.GetColumnNames())
                    {
                        result.Add(item);
                    }

                    return Task.CompletedTask;
                }).ConfigureAwait(false);

            return result;
        }
    }
}
