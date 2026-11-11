using System;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class PostgreSqlOptionsPrecedenceTests : OptionsPrecedenceTests<PostgreSqlSpec>
    {
        protected override Type QueryOptionsType => typeof(NpgsqlQueryOptions);

        protected override Type BulkOptionsType => typeof(NpgsqlBulkOptions);

        protected override QueryOptions OtherProviderQueryOptions(int? commandTimeoutSeconds)
        {
            return new SqlQueryOptions(commandTimeoutSeconds);
        }
    }
}
