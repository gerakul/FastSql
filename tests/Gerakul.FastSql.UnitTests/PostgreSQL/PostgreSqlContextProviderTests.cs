using System;
using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Npgsql;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class PostgreSqlContextProviderTests : ContextProviderTests<PostgreSqlSpec>
    {
        protected override ProviderSpec OtherSpec => new SqlServerSpec();

        protected override Type CommandTextGeneratorType => typeof(NpgsqlCommandTextGenerator);

        protected override Type QueryOptionsType => typeof(NpgsqlQueryOptions);

        protected override Type BulkOptionsType => typeof(NpgsqlBulkOptions);

        protected override Type ConnectionStringContextType => typeof(NpgsqlConnectionStringContext);

        protected override Type ConnectionContextType => typeof(NpgsqlConnectionContext);

        protected override Type TransactionContextType => typeof(NpgsqlTransactionContext);

        protected override Type TransactionType => typeof(NpgsqlTransaction);

        protected override ConnectionStringContext NewConnectionStringContext(ContextProvider provider, string connectionString)
        {
            return new NpgsqlConnectionStringContext((NpgsqlContextProvider)provider, connectionString);
        }

        protected override ConnectionContext NewConnectionContext(ContextProvider provider, DbConnection connection)
        {
            return new NpgsqlConnectionContext((NpgsqlContextProvider)provider, (NpgsqlConnection)connection);
        }

        protected override TransactionContext NewTransactionContext(ContextProvider provider)
        {
            return new NpgsqlTransactionContext((NpgsqlContextProvider)provider, (NpgsqlTransaction)null);
        }
    }
}
