using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A compiled command whose parameters are passed by position. Compile it once with <c>CommandCompilator</c> and
/// give it to <c>CreateSimple</c>.
/// </summary>
public class SimpleCommand
{
    // the names of the first parameters, kept so that they are not formatted for every command
    private static readonly string[] parameterNames = Enumerable.Range(0, 16).Select(x => $"@p{x}").ToArray();

    private readonly ContextProvider contextProvider;

    /// <summary>
    /// The text of the command, or the name of the stored procedure.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Text or stored procedure.
    /// </summary>
    public CommandType CommandType { get; }

    internal SimpleCommand(ContextProvider contextProvider, string commandText,
        CommandType commandType = CommandType.Text)
    {
        ArgumentNullException.ThrowIfNull(commandText);

        this.contextProvider = contextProvider;
        this.CommandText = commandText;
        this.CommandType = commandType;
    }

    internal DbCommand Create(ScopedContext scopedContext, QueryOptions? queryOptions, object?[] parameters)
    {
        if (scopedContext.ContextProvider != contextProvider)
        {
            throw new ArgumentException(Messages.OtherProvider, nameof(scopedContext));
        }

        var cmd = scopedContext.CreateCommand(CommandText);
        cmd.CommandType = CommandType;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] is DbParameter parameter)
            {
                cmd.Parameters.Add(parameter);
            }
            else
            {
                contextProvider.AddParamWithValue(cmd, i < parameterNames.Length ? parameterNames[i] : $"@p{i}", parameters[i] ?? DBNull.Value);
            }
        }

        contextProvider.ApplyQueryOptions(cmd, scopedContext.PrepareQueryOptions(queryOptions));

        return cmd;
    }
}
