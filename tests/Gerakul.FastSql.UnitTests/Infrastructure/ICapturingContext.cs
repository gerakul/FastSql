using System.Collections.Generic;
using System.Data.Common;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    // A scoped context over an unopened connection that keeps every DbCommand it creates.
    // Scoped contexts build the command as soon as a Create* method is called, so the command
    // can be inspected without a database.
    public interface ICapturingContext : ICommandCreator
    {
        ScopedContext Context { get; }
        IReadOnlyList<DbCommand> Commands { get; }
        DbCommand LastCommand { get; }
    }
}
