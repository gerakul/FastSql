using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public class ReadOptions
    {
        public FieldsSelector FieldsSelector { get; set; }
        public FromTypeOption FromTypeOption { get; set; }
        public bool CaseSensitive { get; set; }

        public ReadOptions(FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            this.FieldsSelector = fieldsSelector;
            this.FromTypeOption = fromTypeOption;
            this.CaseSensitive = caseSensitive;
        }
    }
}
