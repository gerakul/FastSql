using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    // U8, PostgreSQL: there is no counterpart of the SQL Server ISqlCommandCreator
    public class NpgsqlCommandCreatorTests
    {
        [Fact]
        public void ContextClasses_ImplementOnlyICommandCreator()
        {
            typeof(NpgsqlConnectionStringContext).GetInterfaces().Should().Equal(typeof(ICommandCreator));
            typeof(NpgsqlConnectionContext).GetInterfaces().Should().Equal(typeof(ICommandCreator));
            typeof(NpgsqlTransactionContext).GetInterfaces().Should().Equal(typeof(ICommandCreator));
        }

        [Fact]
        public void Assembly_DefinesNoPublicInterfaces()
        {
            typeof(NpgsqlContextProvider).Assembly.GetExportedTypes().Where(x => x.IsInterface).Should().BeEmpty();
        }
    }
}
