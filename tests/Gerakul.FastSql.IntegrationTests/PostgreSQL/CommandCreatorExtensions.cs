using System.Linq;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    // The README "Extensibility" example for PostgreSQL: the same code with "limit 10" instead of "top 10"
    // and the quoted names of the PostgreSQL seed
    public static partial class CommandCreatorExtensions
    {
        // making your own custom command accessible from all types of contexts (connection string, connection, transaction)
        public static IWrappedCommand CreateSelectTop10Employees(this ICommandCreator creator)
        {
            return creator.Set(c =>
            {
                var cmd = c.CreateCommand("select * from \"Employee\" limit 10");
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandTimeout = 1000;
                return cmd;
            });
        }

        // combining Create and Execute
        public static T[] GetTable<T>(this ICommandCreator creator, string tableName) where T : new()
        {
            return creator.CreateSimple($"select * from {tableName}").ExecuteQuery<T>().ToArray();
        }
    }
}
