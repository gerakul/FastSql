using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

    internal SqlCommand CreateCommandInternal(string commandText)
    {
      return Transaction == null ? new SqlCommand(commandText, Connection) : new SqlCommand(commandText, Connection, Transaction);
    }

    public SqlCommand CreateCommand(string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommandInternal(commandText);

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
      Helpers.ApplyQueryOptions(cmd, queryOptions);

      return cmd.ExecuteNonQuery();
    }

    public Task<int> ExecuteNonQueryAsync(CancellationToken ct, QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommand(commandText, parameters);
      Helpers.ApplyQueryOptions(cmd, queryOptions);

      return cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<int> ExecuteNonQueryAsync(QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, queryOptions, commandText, parameters);
    }

    public int ExecuteNonQuery(string commandText, params object[] parameters)
    {
      return ExecuteNonQuery(new QueryOptions(), commandText, parameters);
    }

    public Task<int> ExecuteNonQueryAsync(CancellationToken ct, string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(ct, new QueryOptions(), commandText, parameters);
    }

    public Task<int> ExecuteNonQueryAsync(string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, new QueryOptions(), commandText, parameters);
    }

    public SqlDataReader ExecuteReader(QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommand(commandText, parameters);
      Helpers.ApplyQueryOptions(cmd, queryOptions);

      return cmd.ExecuteReader();
    }

    public Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken, QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      SqlCommand cmd = CreateCommand(commandText, parameters);
      Helpers.ApplyQueryOptions(cmd, queryOptions);

      return cmd.ExecuteReaderAsync(cancellationToken);
    }

    public Task<SqlDataReader> ExecuteReaderAsync(QueryOptions queryOptions, string commandText, params object[] parameters)
    {
      return ExecuteReaderAsync(CancellationToken.None, queryOptions, commandText, parameters);
    }

    public SqlDataReader ExecuteReader(string commandText, params object[] parameters)
    {
      return ExecuteReader(new QueryOptions(), commandText, parameters);
    }

    public Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken, string commandText, params object[] parameters)
    {
      return ExecuteReaderAsync(cancellationToken, new QueryOptions(), commandText, parameters);
    }

    public Task<SqlDataReader> ExecuteReaderAsync(string commandText, params object[] parameters)
    {
      return ExecuteReaderAsync(CancellationToken.None, new QueryOptions(), commandText, parameters);
    }

    private AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(CancellationToken executeReaderCT,
      Func<IDataReader, ReadInfo<T>> readInfoGetter, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return Helpers.CreateAsyncEnumerable<T>(
        state => state.ReadInfo.Reader.Close(),

        async (state, ct) =>
        {
          var reader = await ExecuteReaderAsync(executeReaderCT, options.QueryOptions, commandText, parameters).ConfigureAwait(false);
          state.ReadInfo = readInfoGetter(reader);
        });
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

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken executeReaderCT,
      SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateObjects(r), options, commandText, parameters);
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(CancellationToken.None, options, commandText, parameters);
    }

    public IEnumerable<object[]> ExecuteQuery(string commandText, params object[] parameters)
    {
      return ExecuteQuery(new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken executeReaderCT, string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(executeReaderCT, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(CancellationToken.None, new SqlExecutorOptions(), commandText, parameters);
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

    public IAsyncEnumerable<T> ExecuteQueryAsync<T>(CancellationToken executeReaderCT, SqlExecutorOptions options, string commandText, params object[] parameters) where T : new()
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateByType<T>(r, options.ReadOptions), options, commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAsync<T>(SqlExecutorOptions options, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(CancellationToken.None, options, commandText, parameters);
    }

    public IEnumerable<T> ExecuteQuery<T>(string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQuery<T>(new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAsync<T>(CancellationToken executeReaderCT, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(executeReaderCT, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAsync<T>(string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(CancellationToken.None, new SqlExecutorOptions(), commandText, parameters);
    }

    public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      using (SqlDataReader reader = ExecuteReader(options.QueryOptions, commandText, parameters))
      {
        foreach (var item in reader.ReadAllAnonymous(proto, options.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(CancellationToken executeReaderCT, T proto, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, options.ReadOptions), options, commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(CancellationToken.None, proto, options, commandText, parameters);
    }

    public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymous<T>(proto, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(CancellationToken executeReaderCT, T proto, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(executeReaderCT, proto, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(CancellationToken.None, proto, new SqlExecutorOptions(), commandText, parameters);
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

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken executeReaderCT, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateFirstColumn(r), options, commandText, parameters);
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(CancellationToken.None, options, commandText, parameters);
    }

    public IEnumerable ExecuteQueryFirstColumn(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn(new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken executeReaderCT, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(executeReaderCT, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(CancellationToken.None, new SqlExecutorOptions(), commandText, parameters);
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

    public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken executeReaderCT, SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateFirstColumn<T>(r), options, commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(SqlExecutorOptions options, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(CancellationToken.None, options, commandText, parameters);
    }

    public IEnumerable<T> ExecuteQueryFirstColumn<T>(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn<T>(new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken executeReaderCT, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(executeReaderCT, new SqlExecutorOptions(), commandText, parameters);
    }

    public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(CancellationToken.None, new SqlExecutorOptions(), commandText, parameters);
    }

    internal IEnumerable<string> GetTableColumns(string table)
    {
      using (SqlDataReader r = ExecuteReader(string.Format("select top 0 * from {0} with(nolock)", table)))
      {
        foreach (var item in r.GetColumnNames())
        {
          yield return item;
        }
      }
    }

    internal async Task<IList<string>> GetTableColumnsAsync(CancellationToken cancellationToken, string table)
    {
      List<string> result = new List<string>();
      using (SqlDataReader r = await ExecuteReaderAsync(cancellationToken, string.Format("select top 0 * from {0} with(nolock)", table)).ConfigureAwait(false))
      {
        foreach (var item in r.GetColumnNames())
        {
          result.Add(item);
        }
      }

      return result;
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

    public static async Task<int> ExecuteNonQueryAsync(CancellationToken ct, QueryOptions queryOptions, 
      string connectionString, string commandText, params object[] parameters)
    {
      int result;
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync().ConfigureAwait(false);
        SqlExecutor executor = new SqlExecutor(conn);
        result = await executor.ExecuteNonQueryAsync(queryOptions, commandText, parameters).ConfigureAwait(false);
      }

      return result;
    }

    public static Task<int> ExecuteNonQueryAsync(QueryOptions queryOptions,
      string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, queryOptions, connectionString, commandText, parameters);
    }

    public static int ExecuteNonQuery(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteNonQuery(new QueryOptions(), connectionString, commandText, parameters);
    }

    public static Task<int> ExecuteNonQueryAsync(CancellationToken ct, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(ct, new QueryOptions(), connectionString, commandText, parameters);
    }

    public static Task<int> ExecuteNonQueryAsync(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, new QueryOptions(), connectionString, commandText, parameters);
    }

    private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(CancellationToken executeReaderCT,
      Func<IDataReader, ReadInfo<T>> readInfoGetter, SqlExecutorOptions options, 
      string connectionString, string commandText, params object[] parameters)
    {
      return Helpers.CreateAsyncEnumerable<T>(
        state =>
        {
          state.ReadInfo.Reader.Close();
          state.InternalExecutor.Connection.Close();
        },

        async (state, ct) =>
        {
          SqlConnection conn = null;
          try
          {
            conn = new SqlConnection(connectionString);
            await conn.OpenAsync(executeReaderCT).ConfigureAwait(false);
            SqlExecutor executor = new SqlExecutor(conn);
            state.InternalExecutor = executor;
            var reader = await executor.ExecuteReaderAsync(executeReaderCT, options.QueryOptions, commandText, parameters).ConfigureAwait(false);
            state.ReadInfo = readInfoGetter(reader);
          }
          catch
          {
            if (conn != null)
            {
              conn.Close();
            }

            throw;
          }
        });
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

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken executeReaderCT, SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateObjects(r), options, connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(CancellationToken.None, options, connectionString, commandText, parameters);
    }

    public static IEnumerable<object[]> ExecuteQuery(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQuery(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken executeReaderCT, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(executeReaderCT, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAsync(CancellationToken.None, new SqlExecutorOptions(), connectionString, commandText, parameters);
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

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(CancellationToken executeReaderCT, 
      SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateByType<T>(r, options.ReadOptions), options, connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(CancellationToken.None, options, connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQuery<T>(string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQuery<T>(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(CancellationToken executeReaderCT, string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(executeReaderCT, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(string connectionString, string commandText, params object[] parameters) where T : new()
    {
      return ExecuteQueryAsync<T>(CancellationToken.None, new SqlExecutorOptions(), connectionString, commandText, parameters);
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

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(CancellationToken executeReaderCT, T proto,
      SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateAnonymous(r, proto, options.ReadOptions),
        options, connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto,
      SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(CancellationToken.None, proto, options, connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymous(proto, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(CancellationToken executeReaderCT, T proto, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(executeReaderCT, proto, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryAnonymousAsync(CancellationToken.None, proto, new SqlExecutorOptions(), connectionString, commandText, parameters);
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

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken executeReaderCT, SqlExecutorOptions options,
      string connectionString, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateFirstColumn(r), options, connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(SqlExecutorOptions options,
      string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(CancellationToken.None, options, connectionString, commandText, parameters);
    }

    public static IEnumerable ExecuteQueryFirstColumn(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken executeReaderCT, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(executeReaderCT, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync(CancellationToken.None, new SqlExecutorOptions(), connectionString, commandText, parameters);
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

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken executeReaderCT, SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return CreateAsyncEnumerable(executeReaderCT, r => ReadInfoFactory.CreateFirstColumn<T>(r), options, connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(SqlExecutorOptions options, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(CancellationToken.None, options, connectionString, commandText, parameters);
    }

    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumn<T>(new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken executeReaderCT, string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(executeReaderCT, new SqlExecutorOptions(), connectionString, commandText, parameters);
    }

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(string connectionString, string commandText, params object[] parameters)
    {
      return ExecuteQueryFirstColumnAsync<T>(CancellationToken.None, new SqlExecutorOptions(), connectionString, commandText, parameters);
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

    public static async Task UsingTransactionAsync(Func<SqlExecutor, Task> action, SqlConnection connection, IsolationLevel? isolationLevel = null)
    {
      SqlTransaction tran = null;
      try
      {
        tran = isolationLevel.HasValue ? connection.BeginTransaction(isolationLevel.Value) : connection.BeginTransaction();
        await action(new SqlExecutor(tran)).ConfigureAwait(false);
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

    public static async Task UsingTransactionAsync(Func<SqlExecutor, Task> action, string connectionString, IsolationLevel? isolationLevel = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync().ConfigureAwait(false);
        await UsingTransactionAsync(action, conn, isolationLevel).ConfigureAwait(false);
      }
    }

    public static void UsingConnection(Action<SqlExecutor> action, string connectionString)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        action(new SqlExecutor(conn));
      }
    }

    public static async Task UsingConnectionAsync(Func<SqlExecutor, Task> action, string connectionString)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync().ConfigureAwait(false);
        await action(new SqlExecutor(conn)).ConfigureAwait(false);
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

