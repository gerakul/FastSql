namespace Gerakul.FastSql.Common
{
    public class ReadOptions
    {
        public FieldsSelector FieldsSelector { get; set; }
        public FromTypeOption FromTypeOption { get; set; }
        public bool? CaseSensitive { get; set; }

        public ReadOptions(FieldsSelector fieldsSelector = FieldsSelector.Destination, bool? caseSensitive = null, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            this.FieldsSelector = fieldsSelector;
            this.FromTypeOption = fromTypeOption;
            this.CaseSensitive = caseSensitive;
        }

        protected ReadOptions(ReadOptions readOptions)
        {
            this.FieldsSelector = readOptions.FieldsSelector;
            this.FromTypeOption = readOptions.FromTypeOption;
            this.CaseSensitive = readOptions.CaseSensitive;
        }

        public void SetDefaults(ReadOptions defaultOptions)
        {
            if (!CaseSensitive.HasValue)
            {
                CaseSensitive = defaultOptions.CaseSensitive;
            }
        }

        public virtual ReadOptions Clone()
        {
            return new ReadOptions(this);
        }
    }
}
