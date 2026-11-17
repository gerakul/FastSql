using System.Collections.Generic;
using System.Data.Common;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Npgsql;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    public class CapturingNpgsqlConnectionContext : NpgsqlConnectionContext, ICapturingContext
    {
        private readonly List<DbCommand> commands = new List<DbCommand>();

        public CapturingNpgsqlConnectionContext(NpgsqlContextProvider contextProvider)
            : base(contextProvider, new NpgsqlConnection())
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
