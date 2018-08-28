namespace Gerakul.FastSql.Common
{
    public class ReadOptions
    {
        internal static readonly ReadOptions defaultOptions = new ReadOptions(true);

        public FieldsSelector? FieldsSelector { get; set; }
        public FromTypeOption? FromTypeOption { get; set; }
        public bool? CaseSensitive { get; set; }

        public ReadOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitive = null, FromTypeOption? fromTypeOption = null)
        {
            this.FieldsSelector = fieldsSelector;
            this.FromTypeOption = fromTypeOption;
            this.CaseSensitive = caseSensitive;
        }

        internal ReadOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                this.FieldsSelector = Common.FieldsSelector.Destination;
                this.FromTypeOption = Common.FromTypeOption.Default;
                this.CaseSensitive = false;
            }
        }

        private ReadOptions(ReadOptions readOptions)
        {
            this.FieldsSelector = readOptions.FieldsSelector;
            this.FromTypeOption = readOptions.FromTypeOption;
            this.CaseSensitive = readOptions.CaseSensitive;
        }

        internal ReadOptions SetDefaults(ReadOptions defaultOptions)
        {
            if (!FieldsSelector.HasValue)
            {
                FieldsSelector = defaultOptions.FieldsSelector;
            }

            if (!FromTypeOption.HasValue)
            {
                FromTypeOption = defaultOptions.FromTypeOption;
            }

            if (!CaseSensitive.HasValue)
            {
                CaseSensitive = defaultOptions.CaseSensitive;
            }

            return this;
        }

        internal ReadOptions SetDefaults()
        {
            return SetDefaults(defaultOptions);
        }

        public ReadOptions Clone()
        {
            return new ReadOptions(this);
        }
    }
}
