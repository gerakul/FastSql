using Gerakul.FastSql.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlBulkOptions : BulkOptions
    {
        private static readonly NpgsqlBulkOptions defaultOptions = new NpgsqlBulkOptions(true);

        public int? BatchSize { get; set; }
        public int? BulkCopyTimeout { get; set; }
        // ::: public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; }
        public bool? EnableStreaming { get; set; }
        public ColumnDefinitionOptions ColumnDefinitionOptions { get; set; }

        protected override BulkOptions Default => defaultOptions;

        public NpgsqlBulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, /* ::: SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.Default,*/
          FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null, bool? enableStreaming = null,
          bool? createTable = null, ColumnDefinitionOptions columnDefinitionOptions = null, bool? ignoreDataReaderSchemaTable = null,
          bool? checkTableIfNotExistsBeforeCreation = null)
            : base(fieldsSelector, caseSensitiveFieldsMatching, createTable, ignoreDataReaderSchemaTable, checkTableIfNotExistsBeforeCreation)
        {
            this.BatchSize = batchSize;
            this.BulkCopyTimeout = bulkCopyTimeout;
            // ::: this.SqlBulkCopyOptions = sqlBulkCopyOptions;
            this.EnableStreaming = enableStreaming;
            this.ColumnDefinitionOptions = columnDefinitionOptions;
        }

        protected NpgsqlBulkOptions(bool defaultOptions)
            : base(defaultOptions)
        {
            if (defaultOptions)
            {
                this.BatchSize = null;
                this.BulkCopyTimeout = null;
                //this.SqlBulkCopyOptions = SqlBulkCopyOptions.Default;
                this.EnableStreaming = null;
                this.ColumnDefinitionOptions = ColumnDefinitionOptions.Default;
            }
        }

        protected internal NpgsqlBulkOptions(BulkOptions bulkOptions)
            : base(bulkOptions)
        {
            if (bulkOptions is NpgsqlBulkOptions opt)
            {
                this.BatchSize = opt.BatchSize;
                this.BulkCopyTimeout = opt.BulkCopyTimeout;
                // ::: this.SqlBulkCopyOptions = opt.SqlBulkCopyOptions;
                this.EnableStreaming = opt.EnableStreaming;
                this.ColumnDefinitionOptions = opt.ColumnDefinitionOptions;
            }
            else
            {
                this.BatchSize = null;
                this.BulkCopyTimeout = null;
                // ::: this.SqlBulkCopyOptions = null;
                this.EnableStreaming = null;
                this.ColumnDefinitionOptions = null;
            }
        }

        protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            if (!(defaultOptions is NpgsqlBulkOptions opt))
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

            //if (!SqlBulkCopyOptions.HasValue)
            //{
            //    SqlBulkCopyOptions = opt.SqlBulkCopyOptions;
            //}

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
            return new NpgsqlBulkOptions(this);
        }
    }
}
