using System.Data;
using System.Data.Common;

namespace Gerakul.FastSql.Common;

/// <summary>
/// A compiled command whose parameters are filled from a <c>T</c>. Compile it once with <c>CommandCompilator</c>
/// and give it to <c>CreateMapped</c> with every value.
/// </summary>
public class MappedCommand<T>
{
    private readonly record struct ParMap(string ParameterName, FieldSettings<T> Settings);

    private readonly ContextProvider contextProvider;
    private readonly ParMap[] map;

    /// <summary>
    /// The text of the command, or the name of the stored procedure.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Text or stored procedure.
    /// </summary>
    public CommandType CommandType { get; }

    internal MappedCommand(ContextProvider contextProvider, string commandText, IList<string>? parameters,
        IList<FieldSettings<T>> settings, bool caseSensitiveParamsMatching, CommandType commandType = CommandType.Text)
    {
        ArgumentNullException.ThrowIfNull(commandText);
        ArgumentNullException.ThrowIfNull(settings);

        this.contextProvider = contextProvider;
        this.CommandText = commandText;
        this.CommandType = commandType;

        // the checks below treat a missing list as an empty one
        parameters ??= Array.Empty<string>();

        var comparer = caseSensitiveParamsMatching ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        // the names the settings are found by: without the prefix of a parameter
        var names = new string[parameters.Count];

        for (int i = 0; i < names.Length; i++)
        {
            names[i] = contextProvider.GetSqlParameterName(parameters[i]);
        }

        if (Helpers.TryFindDuplicate(names, comparer, out var duplicate))
        {
            throw new ArgumentException($"Parameter '{duplicate}' is listed more than once{Messages.ComparedIgnoringCase(caseSensitiveParamsMatching)}");
        }

        var settingsByName = new Dictionary<string, FieldSettings<T>>(settings.Count, comparer);

        foreach (var item in settings)
        {
            if (!settingsByName.TryAdd(item.Name, item))
            {
                throw new ArgumentException($"More than one field of {Messages.TypeName(typeof(T))} is named '{item.Name}'"
                    + Messages.ComparedIgnoringCase(caseSensitiveParamsMatching));
            }
        }

        map = new ParMap[names.Length];
        List<string?>? unknown = null;

        for (int i = 0; i < names.Length; i++)
        {
            if (settingsByName.TryGetValue(names[i], out var item))
            {
                map[i] = new ParMap(parameters[i], item);
            }
            else
            {
                (unknown ??= []).Add(parameters[i]);
            }
        }

        if (unknown != null)
        {
            throw new ArgumentException($"No field of {Messages.TypeName(typeof(T))} matches the {Messages.Named("parameter", unknown)}", nameof(parameters));
        }
    }

    internal DbCommand Create(ScopedContext scopedContext, T value, QueryOptions? queryOptions)
    {
        return CreatePrepared(scopedContext, value, scopedContext.PrepareQueryOptions(queryOptions));
    }

    // for callers that have prepared the options already (they need the options to compile the command)
    internal DbCommand CreatePrepared(ScopedContext scopedContext, T value, QueryOptions preparedQueryOptions)
    {
        if (scopedContext.ContextProvider != contextProvider)
        {
            throw new ArgumentException(Messages.OtherProvider, nameof(scopedContext));
        }

        var cmd = scopedContext.CreateCommand(CommandText);
        cmd.CommandType = CommandType;

        foreach (var m in map)
        {
            object? paramValue;
            if (m.Settings.DetermineNullByValue)
            {
                var val = m.Settings.Getter(value);
                paramValue = val == null ? contextProvider.GetDbNull(m.Settings.FieldType) : val;
            }
            else
            {
                paramValue = m.Settings.IsNullGetter(value) ? contextProvider.GetDbNull(m.Settings.FieldType) : m.Settings.Getter(value);
            }

            if (paramValue is DbParameter parameter)
            {
                cmd.Parameters.Add(parameter);
            }
            else
            {
                contextProvider.AddParamWithValue(cmd, m.ParameterName, paramValue);
            }
        }

        contextProvider.ApplyQueryOptions(cmd, preparedQueryOptions);

        return cmd;
    }
}
