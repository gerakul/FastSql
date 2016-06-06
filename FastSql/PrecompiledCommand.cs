using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class PrecompiledCommand<T>
  {
    public string CommandText { get; private set; }
    internal ParMap<T>[] Map;

    internal PrecompiledCommand(string commandText, IList<string> parameters, IList<FieldSettings<T>> settings)
    {
      this.CommandText = commandText;

      if (parameters == null || parameters.Count == 0)
      {
        Map = new ParMap<T>[0];
      }

      if (parameters.GroupBy(x => x.GetSqlParameterName().ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Parameter names has been duplicated");
      }

      if (settings.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Setting names has been duplicated");
      }

      Map = parameters
        .Join(settings,
          x => x.GetSqlParameterName().ToLowerInvariant(),
          x => x.Name.ToLowerInvariant(),
          (x, y) => new ParMap<T>() { ParameterName = x, Settings = y }).ToArray();

      if (Map.Length < parameters.Count)
      {
        throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
      }
    }

    public SqlCommand Create(SqlScope scope, T value, QueryOptions queryOptions = null)
    {
      var cmd = scope.CreateCommandInternal(CommandText);

      for (int i = 0; i < Map.Length; i++)
      {
        var m = Map[i];
        object paramValue;
        if (m.Settings.DetermineNullByValue)
        {
          var val = m.Settings.Getter(value);
          paramValue = val == null ? DBNull.Value : val;
        }
        else
        {
          paramValue = m.Settings.IsNullGetter(value) ? DBNull.Value : m.Settings.Getter(value);
        }

        cmd.Parameters.AddWithValue(m.ParameterName, paramValue);
      }

      Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());

      return cmd;
    }

    public int ExecuteNonQuery(string connectionString, T value, QueryOptions queryOptions = null)
    {
      int result = 0;
      SqlScope.UsingConnection(connectionString, scope => result = Create(scope, value).ExecuteNonQuery(queryOptions));
      return result;
    }

    public async Task<int> ExecuteNonQueryAsync(string connectionString, T value, QueryOptions queryOptions = null, 
      CancellationToken cancellationToken = default(CancellationToken))
    {
      int result = 0;
      await SqlScope.UsingConnectionAsync(connectionString, 
        async scope => result = await Create(scope, value).ExecuteNonQueryAsync(queryOptions, cancellationToken));
      return result;
    }

    public object ExecuteScalar(string connectionString, T value, QueryOptions queryOptions = null)
    {
      object result = null;
      SqlScope.UsingConnection(connectionString, scope => Create(scope, value).ExecuteScalar(queryOptions));
      return result;
    }

    public async Task<object> ExecuteScalarAsync(string connectionString, T value, QueryOptions queryOptions = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      object result = null;
      await SqlScope.UsingConnectionAsync(connectionString,
        async scope => result = await Create(scope, value).ExecuteScalarAsync(queryOptions, cancellationToken));
      return result;
    }

    private AEnumerable<AsyncState<R>, R> CreateAsyncEnumerable<R>(CancellationToken executeReaderCancellationToken,
      Func<IDataReader, ReadInfo<R>> readInfoGetter, SqlExecutorOptions options,
      string connectionString, T value)
    {
      return Helpers.CreateAsyncEnumerable<R>(
        state =>
        {
          if (state.ReadInfo != null)
          {
            state.ReadInfo.Reader.Close();
          }

            if (state.InternalConnection != null)
            {
              state.InternalConnection.Close();
            }
        },

        async (state, ct) =>
        {
          SqlConnection conn = null;
          try
          {
            conn = new SqlConnection(connectionString);
            await conn.OpenAsync(executeReaderCancellationToken).ConfigureAwait(false);
            state.InternalConnection = conn;
            var reader = await Create(new SqlScope(conn), value).ExecuteReaderAsync(options?.QueryOptions, executeReaderCancellationToken).ConfigureAwait(false);
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

    public IEnumerable<object[]> ExecuteQuery(string connectionString, T value, SqlExecutorOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value).ExecuteQuery(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(string connectionString, T value, SqlExecutorOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateObjects(r), options, connectionString, value);
    }

    public IEnumerable<R> ExecuteQuery<R>(string connectionString, T value, SqlExecutorOptions options = null) where R : new()
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value).ExecuteQuery<R>(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAsync<R>(string connectionString, T value, SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken)) where R : new()
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateByType<R>(r, options?.ReadOptions), options, connectionString, value);
    }

    public IEnumerable<R> ExecuteQueryAnonymous<R>(R proto, string connectionString, T value, SqlExecutorOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value).ExecuteQueryAnonymous(proto, options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAnonymousAsync<R>(R proto, string connectionString, T value, SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateAnonymous(r, proto, options?.ReadOptions),
        options, connectionString, value);
    }

    public IEnumerable ExecuteQueryFirstColumn(string connectionString, T value, SqlExecutorOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value).ExecuteQueryFirstColumn(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string connectionString, T value, SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options, connectionString, value);
    }

    public IEnumerable<R> ExecuteQueryFirstColumn<R>(string connectionString, T value, SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value).ExecuteQueryFirstColumn<R>(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryFirstColumnAsync<R>(string connectionString, T value, SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn<R>(r), options, connectionString, value);
    }
  }

  internal class ParMap<T>
  {
    public string ParameterName;
    public FieldSettings<T> Settings;
  }
}
