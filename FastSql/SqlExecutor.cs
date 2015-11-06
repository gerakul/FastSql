using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace Gerakul.FastSql
{
  public class SqlExecutor
  {
    public SqlConnection Connection { get; private set; }
    public SqlTransaction Transaction { get; private set; }

    public SqlExecutor(SqlConnection connection)
    {
      this.Connection = connection;
      this.Transaction = null;
    }

    public SqlExecutor(SqlTransaction transaction)
    {
      this.Connection = transaction.Connection;
      this.Transaction = transaction;
    }

    public SqlCommand CreateCommand(string commandText, params object[] parameters)
    {
      SqlCommand cmd = Transaction == null ? new SqlCommand(commandText, Connection) : new SqlCommand(commandText, Connection, Transaction);

      for (int i = 0; i < parameters.Length; i++)
      {
        if (parameters[i] is SqlParameter)
        {
          cmd.Parameters.Add(parameters[i]);
        }
        else
        {
          cmd.Parameters.AddWithValue("@p" + i.ToString(), (object)parameters[i] ?? DBNull.Value);
        }
      }

      return cmd;
    }

    public int ExecuteNonQuery(QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommand(commandText, parameters);
      if (queryOptions.CommandTimeoutSeconds.HasValue)
      {
        cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
      }

      return cmd.ExecuteNonQuery();
    }

    public int ExecuteNonQuery(string commandText, params object[] parameters)
    {
      return ExecuteNonQuery(new QueryOptions(), commandText, parameters);
    }

    public SqlDataReader ExecuteReader(QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommand(commandText, parameters);
      if (queryOptions.CommandTimeoutSeconds.HasValue)
      {
        cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
      }

      return cmd.ExecuteReader();
    }

    public SqlDataReader ExecuteReader(string commandText, params object[] parameters)
    {
      return ExecuteReader(new QueryOptions(), commandText, parameters);
    }

    public IEnumerable<object[]> ExecuteQuery(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAll())
        {
          yield return item;
        }
      }
    }

    public IEnumerable<object[]> ExecuteQuery(string commandText, params object[] parameters)
    {
      return ExecuteQuery(new SqlExecutorOptions(), commandText, parameters);
    }

    public IEnumerable<T> ExecuteQuery<T>(SqlExecutorOptions options, string commandText, params object[] parameters) where T : new()
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAll<T>(options.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IEnumerable<T> ExecuteQuery<T>(string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQuery<T>(new SqlExecutorOptions(), commandText, parameters);
    }

    public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAllAnonymous<T>(proto, options.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymous<T>(proto, new SqlExecutorOptions(), commandText, parameters);
    }

    public IEnumerable ExecuteQueryFirstColumn(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAllFirstColumn())
        {
          yield return item;
        }
      }
    }

    public IEnumerable ExecuteQueryFirstColumn(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn(new SqlExecutorOptions(), commandText, parameters);
    }

    public IEnumerable<T> ExecuteQueryFirstColumn<T>(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAllFirstColumn<T>())
        {
          yield return item;
        }
      }
    }

    public IEnumerable<T> ExecuteQueryFirstColumn<T>(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn<T>(new SqlExecutorOptions(), commandText, parameters);
    }

    #region Static

    public static int ExecuteNonQuery(QueryOptions queryOptions, string connectionString, string commandText, params object[] parameters)
    {
      int result;
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        result = executor.ExecuteNonQuery(queryOptions, commandText, parameters);
      }

      return result;
    }

    public static int ExecuteNonQuery(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteNonQuery(new QueryOptions(), connectionString, commandText, parameters);
    }

    public static IEnumerable<object[]> ExecuteQuery(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        foreach (var item in executor.ExecuteQuery(options, commandText, parameters))
        {
          yield return item;
        }
      }
    }

    public static IEnumerable<object[]> ExecuteQuery(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQuery(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQuery<T>(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters) where T : new()
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        foreach (var item in executor.ExecuteQuery<T>(options, commandText, parameters))
        {
          yield return item;
        }
      }
    }

    public static IEnumerable<T> ExecuteQuery<T>(string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQuery<T>(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        foreach (var item in executor.ExecuteQueryAnonymous<T>(proto, options, commandText, parameters))
        {
          yield return item;
        }
      }
    }

    public static IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymous<T>(proto, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IEnumerable ExecuteQueryFirstColumn(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        foreach (var item in executor.ExecuteQueryFirstColumn(options, commandText, parameters))
        {
          yield return item;
        }
      }
    }

    public static IEnumerable ExecuteQueryFirstColumn(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlExecutor executor = new SqlExecutor(conn);
        foreach (var item in executor.ExecuteQueryFirstColumn<T>(options, commandText, parameters))
        {
          yield return item;
        }
      }
    }

    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn<T>(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static string GetColumnString(Type type, string preffix = null, FromTypeOption option = FromTypeOption.PublicField | FromTypeOption.PublicProperty)
    {
      List<string> cols = new List<string>();

      if ((option & FromTypeOption.PublicField) == FromTypeOption.PublicField)
      {
        cols.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name).ToArray());
      }

      if ((option & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
      {
        cols.AddRange(type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name).ToArray());
      }

      string result;
      if (string.IsNullOrWhiteSpace(preffix))
      {
        result = string.Join(", ", cols);
      }
      else
      {
        result = string.Join(", ", cols.Select(x => preffix + "." + x));
      }

      return result;
    }

    public static string GetColumnString<T>(string preffix = null, FromTypeOption option = FromTypeOption.PublicField | FromTypeOption.PublicProperty)
    {
      return GetColumnString(typeof(T), preffix, option);
    }

    public static void UsingTransaction(Action<SqlExecutor> action, SqlConnection connection, IsolationLevel? isolationLevel = null)
    {
      SqlTransaction tran = null;
      try
      {
        tran = isolationLevel.HasValue ? connection.BeginTransaction(isolationLevel.Value) : connection.BeginTransaction();
        action(new SqlExecutor(tran));
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

    public static void UsingTransaction(Action<SqlExecutor> action, string connectionString, IsolationLevel? isolationLevel = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        UsingTransaction(action, conn, isolationLevel);
      }
    }

    #endregion
  }

  public class SqlExecutorOptions
  {
    public QueryOptions QueryOptions { get; private set; }
    public ReadOptions ReadOptions { get; private set; }

    public SqlExecutorOptions(QueryOptions queryOptions, ReadOptions readOptions = null)
    {
      this.QueryOptions = queryOptions == null ? new QueryOptions() : queryOptions;
      this.ReadOptions = readOptions == null ? new ReadOptions() : readOptions;
    }

    public SqlExecutorOptions(int? commandTimeoutSeconds = null, FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Both)
      : this(new QueryOptions(commandTimeoutSeconds), new ReadOptions(fieldsSelector, caseSensitive, fromTypeOption))
    {
    }
  }

  public class QueryOptions
  {
    public int? CommandTimeoutSeconds { get; set; }

    public QueryOptions(int? commandTimeoutSeconds = null)
    {
      this.CommandTimeoutSeconds = commandTimeoutSeconds;
    }
  }
}

