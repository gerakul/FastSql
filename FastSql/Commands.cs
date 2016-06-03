using System;
using System.Collections.Generic;
using System.Data;
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

    public static void UsingConnection(string connectionString, Action<SqlConnection> action)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        action(conn);
      }
    }

    public static async Task UsingConnectionAsync(string connectionString, Func<SqlConnection, Task> action)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync().ConfigureAwait(false);
        await action(conn).ConfigureAwait(false);
      }
    }

    public static void UsingTransaction(SqlConnection connection, IsolationLevel isolationLevel, Action<SqlTransaction> action)
    {
      SqlTransaction tran = null;
      try
      {
        tran = connection.BeginTransaction(isolationLevel);
        action(tran);
        tran.Commit();
      }
      catch
      {
        if (tran != null)
        {
          tran.Rollback();
        }

        throw;
      }
    }

    public static void UsingTransaction(SqlConnection connection, Action<SqlTransaction> action)
    {
      UsingTransaction(connection, IsolationLevel.Unspecified, action);
    }

    public static async Task UsingTransactionAsync(SqlConnection connection, IsolationLevel isolationLevel, Func<SqlTransaction, Task> action)
    {
      SqlTransaction tran = null;
      try
      {
        tran = connection.BeginTransaction(isolationLevel);
        await action(tran).ConfigureAwait(false);
        tran.Commit();
      }
      catch
      {
        if (tran != null)
        {
          tran.Rollback();
        }

        throw;
      }
    }

    public static Task UsingTransactionAsync(SqlConnection connection, Func<SqlTransaction, Task> action)
    {
      return UsingTransactionAsync(connection, IsolationLevel.Unspecified, action);
    }

    public static void UsingTransaction(string connectionString, IsolationLevel isolationLevel, Action<SqlTransaction> action)
    {
      UsingConnection(connectionString, conn => UsingTransaction(conn, isolationLevel, action));
    }

    public static void UsingTransaction(string connectionString, Action<SqlTransaction> action)
    {
      UsingTransaction(connectionString, IsolationLevel.Unspecified, action);
    }

    public static Task UsingTransactionAsync(string connectionString, IsolationLevel isolationLevel, Func<SqlTransaction, Task> action)
    {
      return UsingConnectionAsync(connectionString, conn => UsingTransactionAsync(conn, action));
    }

    public static Task UsingTransactionAsync(string connectionString, Func<SqlTransaction, Task> action)
    {
      return UsingTransactionAsync(connectionString, IsolationLevel.Unspecified, action);
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
