using System.Data.Common;
using System.Data.SqlClient;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.SqlServer;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerContextLifecycleTests : ContextLifecycleTests<SqlServerDatabase>
    {
        public SqlServerContextLifecycleTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string CurrentIsolationLevelCommandText =>
            "select case transaction_isolation_level when 0 then 'unspecified' when 1 then 'read uncommitted' "
            + "when 2 then 'read committed' when 3 then 'repeatable read' when 4 then 'serializable' when 5 then 'snapshot' end "
            + "from sys.dm_exec_sessions where session_id = @@SPID";

        protected override TransactionContext CreateTransactionContextByConstructor(DbTransaction transaction)
        {
            return new SqlTransactionContext(SqlContextProvider.DefaultInstance, (SqlTransaction)transaction);
        }

        protected override string ClosedConnectionMessage(string operation)
        {
            switch (operation)
            {
                case "BeginTransaction":
                    return "Invalid operation. The connection is closed.";
                // the async methods report the name of their Begin* implementation
                case "ExecuteNonQueryAsync":
                    return "BeginExecuteNonQuery requires an open and available Connection. The connection's current state is closed.";
                case "ExecuteReaderAsync":
                    return "BeginExecuteReader requires an open and available Connection. The connection's current state is closed.";
                default:
                    return operation + " requires an open and available Connection. The connection's current state is closed.";
            }
        }
    }
}
