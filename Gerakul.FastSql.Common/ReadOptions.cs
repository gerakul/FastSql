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

        public void SetDefaults(ReadOptions defaultOptions)
        {
            if (!CaseSensitive.HasValue)
            {
                CaseSensitive = defaultOptions.CaseSensitive;
            }
        }

        public ReadOptions Clone()
        {
            return new ReadOptions(FieldsSelector, CaseSensitive, FromTypeOption);
        }
    }
}
