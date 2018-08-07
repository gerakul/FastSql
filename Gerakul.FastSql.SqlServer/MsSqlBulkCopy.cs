using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.SqlServer
{
    internal class MsSqlBulkCopy : BulkCopy
    {
        private class Mapping
        {
            public string Source;
            public string Destination;
        }

        private SqlConnection Connection
        {
            get
            {
                if (context is TransactionContext)
                {
                    return (SqlConnection)((TransactionContext)context).Transaction.Connection;
                }
                else if (context is ConnectionContext)
                {
                    return (SqlConnection)((ConnectionContext)context).Connection;
                }
                else
                {
                    return null;
                }
            }
        }

        private SqlTransaction Transaction
        {
            get
            {
                if (context is TransactionContext)
                {
                    return (SqlTransaction)((TransactionContext)context).Transaction;
                }
                else
                {
                    return null;
                }
            }
        }

        private SqlBulkOptions SqlBulkOptions => (SqlBulkOptions)bulkOptions;

        internal MsSqlBulkCopy(ScopedContext context, SqlBulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields)
            : base(context, bulkOptions, destinationTable, reader, fields)
        {
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
                using (SqlBulkCopy bcp = new SqlBulkCopy(Connection, SqlBulkOptions.SqlBulkCopyOptions, Transaction))
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
                using (SqlBulkCopy bcp = new SqlBulkCopy(Connection, SqlBulkOptions.SqlBulkCopyOptions, Transaction))
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

                    await bcp.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private string GetCreateTableScript(string[] sourceFields)
        {
            var colDefs = (bulkOptions.IgnoreDataReaderSchemaTable.HasValue ?
                    reader.GetColumnDefinitions(SqlBulkOptions.ColumnDefinitionOptions, bulkOptions.IgnoreDataReaderSchemaTable.Value)
                    : reader.GetColumnDefinitions(SqlBulkOptions.ColumnDefinitionOptions))
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
            context.CreateSimple(cmd).ExecuteNonQuery();
        }

        private async Task CreateTableAsync(string[] sourceFields)
        {
            string cmd = GetCreateTableScript(sourceFields);
            await context.CreateSimple(cmd).ExecuteNonQueryAsync();
        }

        private IList<string> GetTableColumns()
        {
            List<string> result = new List<string>();
            using (DbDataReader r = context.CreateSimple(string.Format("select top 0 * from {0} with(nolock)", destinationTable))
                .Unwrap().ExecuteReader())
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
            List<string> result = new List<string>();
            using (DbDataReader r = await context.CreateSimple(string.Format("select top 0 * from {0} with(nolock)", destinationTable))
                .Unwrap().ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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
