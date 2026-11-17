using System;
using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Microsoft.Data.SqlClient;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlServerContextProviderTests : ContextProviderTests<SqlServerSpec>
    {
        protected override ProviderSpec OtherSpec => new PostgreSqlSpec();

        protected override Type CommandTextGeneratorType => typeof(SqlCommandTextGenerator);

        protected override Type QueryOptionsType => typeof(SqlQueryOptions);

        protected override Type BulkOptionsType => typeof(SqlBulkOptions);

        protected override Type ConnectionStringContextType => typeof(SqlConnectionStringContext);

        protected override Type ConnectionContextType => typeof(SqlConnectionContext);

        protected override Type TransactionContextType => typeof(SqlTransactionContext);

        protected override Type TransactionType => typeof(SqlTransaction);

        protected override ConnectionStringContext NewConnectionStringContext(ContextProvider provider, string connectionString)
        {
            return new SqlConnectionStringContext((SqlContextProvider)provider, connectionString);
        }

        protected override ConnectionContext NewConnectionContext(ContextProvider provider, DbConnection connection)
        {
            return new SqlConnectionContext((SqlContextProvider)provider, (SqlConnection)connection);
        }

        protected override TransactionContext NewTransactionContext(ContextProvider provider)
        {
            return new SqlTransactionContext((SqlContextProvider)provider, (SqlTransaction)null);
        }
    }
}
