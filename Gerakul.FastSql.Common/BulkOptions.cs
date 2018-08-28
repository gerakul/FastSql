namespace Gerakul.FastSql.Common
{
    public class BulkOptions
    {
        private static readonly BulkOptions defaultOptions = new BulkOptions(true);

        public FieldsSelector? FieldsSelector { get; set; }
        public bool? CaseSensitive { get; set; }
        public bool? CreateTable { get; set; }
        public bool? IgnoreDataReaderSchemaTable { get; set; }
        public bool? CheckTableIfNotExistsBeforeCreation { get; set; }

        protected virtual BulkOptions Default => defaultOptions;

        public BulkOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitive = null,
          bool? createTable = null, bool? ignoreDataReaderSchemaTable = null,
          bool? checkTableIfNotExistsBeforeCreation = null)
        {
            this.FieldsSelector = fieldsSelector;
            this.CaseSensitive = caseSensitive;
            this.CreateTable = createTable;
            this.IgnoreDataReaderSchemaTable = ignoreDataReaderSchemaTable;
            this.CheckTableIfNotExistsBeforeCreation = checkTableIfNotExistsBeforeCreation;
        }

        protected BulkOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                this.FieldsSelector = Common.FieldsSelector.Source;
                this.CaseSensitive = null;
                this.CreateTable = false;
                this.IgnoreDataReaderSchemaTable = false;
                this.CheckTableIfNotExistsBeforeCreation = false;
            }
        }

        protected BulkOptions(BulkOptions bulkOptions)
        {
            this.FieldsSelector = bulkOptions.FieldsSelector;
            this.CaseSensitive = bulkOptions.CaseSensitive;
            this.CreateTable = bulkOptions.CreateTable;
            this.IgnoreDataReaderSchemaTable = bulkOptions.IgnoreDataReaderSchemaTable;
            this.CheckTableIfNotExistsBeforeCreation = bulkOptions.CheckTableIfNotExistsBeforeCreation;
        }

        protected internal virtual BulkOptions SetDefaults(BulkOptions defaultOptions)
        {
            if (!FieldsSelector.HasValue)
            {
                FieldsSelector = defaultOptions.FieldsSelector;
            }

            if (!CaseSensitive.HasValue)
            {
                CaseSensitive = defaultOptions.CaseSensitive;
            }

            if (!CreateTable.HasValue)
            {
                CreateTable = defaultOptions.CreateTable;
            }

            if (!IgnoreDataReaderSchemaTable.HasValue)
            {
                IgnoreDataReaderSchemaTable = defaultOptions.IgnoreDataReaderSchemaTable;
            }

            if (!CheckTableIfNotExistsBeforeCreation.HasValue)
            {
                CheckTableIfNotExistsBeforeCreation = defaultOptions.CheckTableIfNotExistsBeforeCreation;
            }

            return this;
        }

        internal BulkOptions SetDefaults()
        {
            return SetDefaults(Default);
        }

        public virtual BulkOptions Clone()
        {
            return new BulkOptions(this);
        }
    }
}
