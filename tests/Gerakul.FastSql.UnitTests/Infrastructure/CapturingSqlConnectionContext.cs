using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    public class CapturingSqlConnectionContext : SqlConnectionContext, ICapturingContext
    {
        private readonly List<DbCommand> commands = new List<DbCommand>();

        public CapturingSqlConnectionContext(SqlContextProvider contextProvider)
            : base(contextProvider, new SqlConnection())
        {
        }

        public ScopedContext Context => this;

        public IReadOnlyList<DbCommand> Commands => commands;

        public DbCommand LastCommand => commands.Count == 0 ? null : commands[commands.Count - 1];

        public override DbCommand CreateCommand(string commandText)
        {
            var command = base.CreateCommand(commandText);
            commands.Add(command);
            return command;
        }
    }
}
