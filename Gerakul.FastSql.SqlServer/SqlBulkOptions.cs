using Gerakul.FastSql.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlBulkOptions : BulkOptions
    {
        public int? BatchSize { get; set; }
        public int? BulkCopyTimeout { get; set; }
        public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; }
        public bool? EnableStreaming { get; set; }
        public ColumnDefinitionOptions ColumnDefinitionOptions { get; set; }

        public SqlBulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.Default,
          FieldsSelector fieldsSelector = FieldsSelector.Source, bool? caseSensitive = null, bool? enableStreaming = null,
          bool createTable = false, ColumnDefinitionOptions columnDefinitionOptions = null, bool? ignoreDataReaderSchemaTable = null,
          bool? checkTableIfNotExistsBeforeCreation = null)
            : base(fieldsSelector, caseSensitive, createTable, ignoreDataReaderSchemaTable, checkTableIfNotExistsBeforeCreation)
        {
            this.BatchSize = batchSize;
            this.BulkCopyTimeout = bulkCopyTimeout;
            this.SqlBulkCopyOptions = sqlBulkCopyOptions;
            this.EnableStreaming = enableStreaming;
            this.ColumnDefinitionOptions = columnDefinitionOptions;
        }

        internal static SqlBulkOptions FromBulkOptions(BulkOptions bulkOptions)
        {
            if (bulkOptions is SqlBulkOptions)
            {
                return (SqlBulkOptions)bulkOptions;
            }
            else
            {
                return new SqlBulkOptions(fieldsSelector: bulkOptions.FieldsSelector,
                    caseSensitive: bulkOptions.CaseSensitive,
                    createTable: bulkOptions.CreateTable,
                    ignoreDataReaderSchemaTable: bulkOptions.IgnoreDataReaderSchemaTable,
                    checkTableIfNotExistsBeforeCreation: bulkOptions.CheckTableIfNotExistsBeforeCreation);
            }
        }

        public override void SetDefaults(BulkOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            var opt = defaultOptions as SqlBulkOptions;
            if (opt == null)
            {
                return;
            }

            if (!BatchSize.HasValue)
            {
                BatchSize = opt.BatchSize;
            }

            if (!BulkCopyTimeout.HasValue)
            {
                BulkCopyTimeout = opt.BulkCopyTimeout;
            }

            if (!EnableStreaming.HasValue)
            {
                EnableStreaming = opt.EnableStreaming;
            }

            if (ColumnDefinitionOptions == null)
            {
                ColumnDefinitionOptions = opt.ColumnDefinitionOptions?.Clone();
            }
        }
    }
}
