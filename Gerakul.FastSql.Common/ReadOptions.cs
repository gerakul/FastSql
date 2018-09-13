namespace Gerakul.FastSql.Common
{
    public class ReadOptions
    {
        internal static readonly ReadOptions defaultOptions = new ReadOptions(true);

        public FieldsSelector? FieldsSelector { get; set; }
        public FromTypeOption? FromTypeOption { get; set; }
        public bool? CaseSensitiveFieldsMatching { get; set; }

        public ReadOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null, FromTypeOption? fromTypeOption = null)
        {
            this.FieldsSelector = fieldsSelector;
            this.FromTypeOption = fromTypeOption;
            this.CaseSensitiveFieldsMatching = caseSensitiveFieldsMatching;
        }

        internal ReadOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                this.FieldsSelector = Common.FieldsSelector.Destination;
                this.FromTypeOption = Common.FromTypeOption.Default;
                this.CaseSensitiveFieldsMatching = false;
            }
        }

        private ReadOptions(ReadOptions readOptions)
        {
            this.FieldsSelector = readOptions.FieldsSelector;
            this.FromTypeOption = readOptions.FromTypeOption;
            this.CaseSensitiveFieldsMatching = readOptions.CaseSensitiveFieldsMatching;
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

            if (!CaseSensitiveFieldsMatching.HasValue)
            {
                CaseSensitiveFieldsMatching = defaultOptions.CaseSensitiveFieldsMatching;
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
