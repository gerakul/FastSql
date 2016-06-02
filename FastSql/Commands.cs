using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public static class Commands
  {
    private static Regex regName = new Regex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline);

    private static string[] ParseCommandText(string commandText)
    {
      return regName.Matches(commandText).Cast<Match>()
        .Select(x => "@" + x.Groups["Name"].ToString().ToLowerInvariant())
        .Distinct().ToArray();
    }

    private static SqlParameter CreateParameter(string name)
    {
      var p = new SqlParameter();
      p.ParameterName = name;
      return p;
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, IList<SqlParameter> parameters, IList<FieldSettings<T>> settings)
    {
      return new PrecompiledCommand<T>(commandText, parameters, settings);
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, IEnumerable<string> paramNames, IList<FieldSettings<T>> settings)
    {
      return new PrecompiledCommand<T>(commandText, paramNames.Select(x => CreateParameter(x)).ToArray(), settings);
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, IList<FieldSettings<T>> settings)
    {
      return new PrecompiledCommand<T>(commandText, ParseCommandText(commandText).Select(x => CreateParameter(x)).ToArray(), settings);
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, IList<SqlParameter> parameters, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, parameters, FieldSettings.FromType<T>(option));
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, IEnumerable<string> paramNames, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, paramNames.Select(x => CreateParameter(x)).ToArray(), FieldSettings.FromType<T>(option));
    }

    public static PrecompiledCommand<T> Compile<T>(string commandText, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, ParseCommandText(commandText).Select(x => CreateParameter(x)).ToArray(), FieldSettings.FromType<T>(option));
    }

    public static PrecompiledCommand<T> Compile<T>(T proto, string commandText, IList<SqlParameter> parameters, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, parameters, FieldSettings.FromType(proto, option));
    }

    public static PrecompiledCommand<T> Compile<T>(T proto, string commandText, IEnumerable<string> paramNames, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, paramNames.Select(x => CreateParameter(x)).ToArray(), FieldSettings.FromType(proto, option));
    }

    public static PrecompiledCommand<T> Compile<T>(T proto, string commandText, FromTypeOption option = FromTypeOption.Both)
    {
      return new PrecompiledCommand<T>(commandText, ParseCommandText(commandText).Select(x => CreateParameter(x)).ToArray(), FieldSettings.FromType(proto, option));
    }
  }
}
