using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Core
{
    public abstract class BulkOptions
    {
        public FieldsSelector FieldsSelector { get; set; }
        public bool? CaseSensitive { get; set; }
        public bool CreateTable { get; set; } = false;
        public bool? IgnoreDataReaderSchemaTable { get; set; }
        public bool? CheckTableIfNotExistsBeforeCreation { get; set; }

        public BulkOptions(FieldsSelector fieldsSelector = FieldsSelector.Source, bool? caseSensitive = null,
          bool createTable = false, bool? ignoreDataReaderSchemaTable = null,
          bool? checkTableIfNotExistsBeforeCreation = null)
        {
            this.FieldsSelector = fieldsSelector;
            this.CaseSensitive = caseSensitive;
            this.CreateTable = createTable;
            this.IgnoreDataReaderSchemaTable = ignoreDataReaderSchemaTable;
            this.CheckTableIfNotExistsBeforeCreation = checkTableIfNotExistsBeforeCreation;
        }
    }
}
