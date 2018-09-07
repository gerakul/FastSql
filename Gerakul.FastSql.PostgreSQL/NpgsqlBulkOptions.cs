using Gerakul.FastSql.Common;
using Npgsql;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlBulkOptions : BulkOptions
    {
        private static readonly NpgsqlBulkOptions defaultOptions = new NpgsqlBulkOptions(true);

        protected override BulkOptions Default => defaultOptions;

        public NpgsqlBulkOptions(FieldsSelector? fieldsSelector = null, bool? caseSensitiveFieldsMatching = null)
            : base(fieldsSelector, caseSensitiveFieldsMatching, null, null, null)
        {
        }

        protected NpgsqlBulkOptions(bool defaultOptions)
            : base(defaultOptions)
        {
        }

        protected internal NpgsqlBulkOptions(BulkOptions bulkOptions)
            : base(bulkOptions)
        {
        }

        protected override BulkOptions SetDefaults(BulkOptions defaultOptions)
        {
            base.SetDefaults(defaultOptions);
            return this;
        }

        public override BulkOptions Clone()
        {
            return new NpgsqlBulkOptions(this);
        }
    }
}
