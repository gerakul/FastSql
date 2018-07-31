using Gerakul.FastSql.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    internal class MsSqlBulkCopy : BulkCopy
    {
        private class Mapping
        {
            public string Source;
            public string Destination;
        }

        private SqlConnection connection;
        private SqlTransaction transaction;
        private SqlBulkOptions bulkOptions;
        private string destinationTable;
        private DbDataReader reader;
        private string[] fields;

        public MsSqlBulkCopy(SqlConnection connection, SqlTransaction transaction, SqlBulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields)
        {
            this.connection = connection;
            this.transaction = transaction;
            this.bulkOptions = bulkOptions;
            this.destinationTable = destinationTable;
            this.reader = reader;
            this.fields = fields;
        }

        public override void WriteToServer()
        {
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

            if (bulkOptions.CreateTable)
            {
                CreateTable(sourceFields);
            }

            Mapping[] map;

            if (bulkOptions.FieldsSelector == FieldsSelector.Source && !bulkOptions.CaseSensitive.HasValue)
            {
                map = sourceFields.Select(x => new Mapping() { Source = x, Destination = x }).ToArray();
            }
            else
            {
                string[] destFields = GetTableColumns().ToArray();

                map = bulkOptions.CaseSensitive.HasValue && bulkOptions.CaseSensitive.Value ?
                  sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping() { Source = x, Destination = y }).ToArray() :
                  sourceFields.Join(destFields, x => x.ToLowerInvariant(), x => x.ToLowerInvariant(), (x, y) => new Mapping() { Source = x, Destination = y }).ToArray();

                Helpers.CheckFieldSelection(bulkOptions.FieldsSelector, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                using (SqlBulkCopy bcp = new SqlBulkCopy(connection, bulkOptions.SqlBulkCopyOptions, transaction))
                {
                    bcp.DestinationTableName = destinationTable;
                    if (bulkOptions.BatchSize.HasValue)
                    {
                        bcp.BatchSize = bulkOptions.BatchSize.Value;
                    }

                    if (bulkOptions.BulkCopyTimeout.HasValue)
                    {
                        bcp.BulkCopyTimeout = bulkOptions.BulkCopyTimeout.Value;
                    }

                    if (bulkOptions.EnableStreaming.HasValue)
                    {
                        bcp.EnableStreaming = bulkOptions.EnableStreaming.Value;
                    }

                    foreach (var item in map)
                    {
                        bcp.ColumnMappings.Add(item.Source, item.Destination);
                    }

                    bcp.WriteToServer(reader);
                }
            }
        }

        public override async Task WriteToServerAsync(CancellationToken cancellationToken)
        {
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

            if (bulkOptions.CreateTable)
            {
                await CreateTableAsync(sourceFields);
            }

            Mapping[] map;

            if (bulkOptions.FieldsSelector == FieldsSelector.Source && !bulkOptions.CaseSensitive.HasValue)
            {
                map = sourceFields.Select(x => new Mapping() { Source = x, Destination = x }).ToArray();
            }
            else
            {
                string[] destFields = (await GetTableColumnsAsync(cancellationToken).ConfigureAwait(false)).ToArray();

                map = bulkOptions.CaseSensitive.HasValue && bulkOptions.CaseSensitive.Value ?
                  sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping() { Source = x, Destination = y }).ToArray() :
                  sourceFields.Join(destFields, x => x.ToLowerInvariant(), x => x.ToLowerInvariant(), (x, y) => new Mapping() { Source = x, Destination = y }).ToArray();

                Helpers.CheckFieldSelection(bulkOptions.FieldsSelector, sourceFields.Length, destFields.Length, map.Length);
            }

            if (map.Length > 0)
            {
                using (SqlBulkCopy bcp = new SqlBulkCopy(connection, bulkOptions.SqlBulkCopyOptions, transaction))
                {
                    bcp.DestinationTableName = destinationTable;
                    if (bulkOptions.BatchSize.HasValue)
                    {
                        bcp.BatchSize = bulkOptions.BatchSize.Value;
                    }

                    if (bulkOptions.BulkCopyTimeout.HasValue)
                    {
                        bcp.BulkCopyTimeout = bulkOptions.BulkCopyTimeout.Value;
                    }

                    if (bulkOptions.EnableStreaming.HasValue)
                    {
                        bcp.EnableStreaming = bulkOptions.EnableStreaming.Value;
                    }

                    foreach (var item in map)
                    {
                        bcp.ColumnMappings.Add(item.Source, item.Destination);
                    }

                    await bcp.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private string GetCreateTableScript(string[] sourceFields)
        {
            var colDefs = (bulkOptions.IgnoreDataReaderSchemaTable.HasValue ?
                    reader.GetColumnDefinitions(bulkOptions.ColumnDefinitionOptions, bulkOptions.IgnoreDataReaderSchemaTable.Value)
                    : reader.GetColumnDefinitions(bulkOptions.ColumnDefinitionOptions))
                .Where(x => sourceFields.Contains(x.Name)).ToArray();

            if (colDefs.Length != sourceFields.Length)
            {
                throw new InvalidOperationException("Can not create table with specified fields");
            }

            return bulkOptions.CheckTableIfNotExistsBeforeCreation.HasValue ?
                colDefs.CreateTableScript(destinationTable, bulkOptions.CheckTableIfNotExistsBeforeCreation.Value)
                : colDefs.CreateTableScript(destinationTable);
        }

        private void CreateTable(string[] sourceFields)
        {
            string cmd = GetCreateTableScript(sourceFields);
            SqlScope scope = transaction == null ? new SqlScope(connection) : new SqlScope(transaction);
            scope.CreateSimple(cmd).ExecuteNonQuery();
        }

        private async Task CreateTableAsync(string[] sourceFields)
        {
            string cmd = GetCreateTableScript(sourceFields);
            SqlScope scope = transaction == null ? new SqlScope(connection) : new SqlScope(transaction);
            await scope.CreateSimple(cmd).ExecuteNonQueryAsync();
        }

        private IList<string> GetTableColumns()
        {
            SqlScope scope = transaction == null ? new SqlScope(connection) : new SqlScope(transaction);
            List<string> result = new List<string>();
            using (DbDataReader r = scope.CreateSimple(string.Format("select top 0 * from {0} with(nolock)", destinationTable)).ExecuteReader())
            {
                foreach (var item in r.GetColumnNames())
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private async Task<IList<string>> GetTableColumnsAsync(CancellationToken cancellationToken)
        {
            SqlScope scope = transaction == null ? new SqlScope(connection) : new SqlScope(transaction);
            List<string> result = new List<string>();
            using (DbDataReader r = await scope.CreateSimple(string.Format("select top 0 * from {0} with(nolock)", destinationTable)).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var item in r.GetColumnNames())
                {
                    result.Add(item);
                }
            }

            return result;
        }
    }
}
