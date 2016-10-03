using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    internal class BulkCopy
    {
        private class Mapping
        {
            public string Source;
            public string Destination;
        }

        private SqlConnection connection;
        private SqlTransaction transaction;
        private BulkOptions bulkOptions;
        private string destinationTable;
        private IDataReader reader;
        private string[] fields;

        public BulkCopy(SqlConnection connection, SqlTransaction transaction, BulkOptions bulkOptions, string destinationTable, IDataReader reader, params string[] fields)
        {
            this.connection = connection;
            this.transaction = transaction;
            this.bulkOptions = bulkOptions;
            this.destinationTable = destinationTable;
            this.reader = reader;
            this.fields = fields;
        }

        public void WriteToServer()
        {
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

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

        public async Task WriteToServerAsync(CancellationToken cancellationToken)
        {
            string[] sourceFields = fields.Length > 0 ? fields.ToArray() : reader.GetColumnNames().ToArray();

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

        public Task WriteToServerAsync()
        {
            return WriteToServerAsync(CancellationToken.None);
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

    public class BulkOptions
    {
        public int? BatchSize { get; set; }
        public int? BulkCopyTimeout { get; set; }
        public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; }
        public FieldsSelector FieldsSelector { get; set; }
        public bool? CaseSensitive { get; set; }
        public bool? EnableStreaming { get; set; }

        public BulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.Default,
          FieldsSelector fieldsSelector = FieldsSelector.Source, bool? caseSensitive = null, bool? enableStreaming = null)
        {
            this.BatchSize = batchSize;
            this.BulkCopyTimeout = bulkCopyTimeout;
            this.SqlBulkCopyOptions = sqlBulkCopyOptions;
            this.FieldsSelector = fieldsSelector;
            this.CaseSensitive = caseSensitive;
            this.EnableStreaming = enableStreaming;
        }
    }
}
