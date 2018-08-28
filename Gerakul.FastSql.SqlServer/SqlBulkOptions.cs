using Gerakul.FastSql.Common;
using System.Data.SqlClient;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlBulkOptions : BulkOptions
    {
        private static readonly SqlBulkOptions defaultOptions = new SqlBulkOptions(true);

        public int? BatchSize { get; set; }
        public int? BulkCopyTimeout { get; set; }
        public SqlBulkCopyOptions? SqlBulkCopyOptions { get; set; }
        public bool? EnableStreaming { get; set; }
        public ColumnDefinitionOptions ColumnDefinitionOptions { get; set; }

        protected override BulkOptions Default => defaultOptions;

        public SqlBulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions? sqlBulkCopyOptions = null,
          FieldsSelector? fieldsSelector = null, bool? caseSensitive = null, bool? enableStreaming = null,
          bool? createTable = null, ColumnDefinitionOptions columnDefinitionOptions = null, bool? ignoreDataReaderSchemaTable = null,
          bool? checkTableIfNotExistsBeforeCreation = null)
            : base(fieldsSelector, caseSensitive, createTable, ignoreDataReaderSchemaTable, checkTableIfNotExistsBeforeCreation)
        {
            this.BatchSize = batchSize;
            this.BulkCopyTimeout = bulkCopyTimeout;
            this.SqlBulkCopyOptions = sqlBulkCopyOptions;
            this.EnableStreaming = enableStreaming;
            this.ColumnDefinitionOptions = columnDefinitionOptions;
        }

        protected SqlBulkOptions(bool defaultOptions)
            : base(defaultOptions)
        {
            if (defaultOptions)
            {
                this.BatchSize = null;
                this.BulkCopyTimeout = null;
                this.SqlBulkCopyOptions = System.Data.SqlClient.SqlBulkCopyOptions.Default;
                this.EnableStreaming = null;
                this.ColumnDefinitionOptions = ColumnDefinitionOptions.Default;
            }
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
                this.SqlBulkCopyOptions = null;
                this.EnableStreaming = null;
                this.ColumnDefinitionOptions = null;
            }
        }

        protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            if (!(defaultOptions is SqlBulkOptions opt))
            {
                return this;
            }

            if (!BatchSize.HasValue)
            {
                BatchSize = opt.BatchSize;
            }

            if (!BulkCopyTimeout.HasValue)
            {
                BulkCopyTimeout = opt.BulkCopyTimeout;
            }

            if (!SqlBulkCopyOptions.HasValue)
            {
                SqlBulkCopyOptions = opt.SqlBulkCopyOptions;
            }

            if (!EnableStreaming.HasValue)
            {
                EnableStreaming = opt.EnableStreaming;
            }

            if (ColumnDefinitionOptions == null)
            {
                ColumnDefinitionOptions = opt.ColumnDefinitionOptions?.Clone();
            }

            return this;
        }

        public override BulkOptions Clone()
        {
            return new SqlBulkOptions(this);
        }
    }
}
