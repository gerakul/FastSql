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

        protected internal SqlBulkOptions(BulkOptions bulkOptions)
            : base(bulkOptions)
        {
            if (bulkOptions is SqlBulkOptions opt)
            {
                this.BatchSize = opt.BatchSize;
                this.BulkCopyTimeout = opt.BulkCopyTimeout;
                this.SqlBulkCopyOptions = opt.SqlBulkCopyOptions;
                this.EnableStreaming = opt.EnableStreaming;
                this.ColumnDefinitionOptions = opt.ColumnDefinitionOptions;
            }
            else
            {
                this.BatchSize = null;
                this.BulkCopyTimeout = null;
                this.SqlBulkCopyOptions = SqlBulkCopyOptions.Default;
                this.EnableStreaming = null;
                this.ColumnDefinitionOptions = null;
            }
        }

        public override void SetDefaults(BulkOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            if (!(defaultOptions is SqlBulkOptions opt))
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

        public override BulkOptions Clone()
        {
            return new SqlBulkOptions(this);
        }
    }
}
