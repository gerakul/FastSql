using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class SimpleCommand
  {
    public string CommandText { get; private set; }
    private object[] parameters;

    private SimpleCommand(string commandText, object[] parameters)
    {
      this.CommandText = commandText;
      this.parameters = parameters;
    }

    public DbCommand Create(IDbScope scope, QueryOptions queryOptions = null)
    {
      var cmd = scope.CreateCommand(CommandText);

      for (int i = 0; i < parameters.Length; i++)
      {
        if (parameters[i] is DbParameter)
        {
          cmd.Parameters.Add(parameters[i]);
        }
        else
        {
          scope.AddParamWithValue(cmd, "@p" + i.ToString(), (object)parameters[i] ?? DBNull.Value);
        }
      }

      Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());

      return cmd;
    }

    #region Execution

    public int ExecuteNonQuery(string connectionString, QueryOptions queryOptions = null)
    {
      int result = 0;
      SqlScope.UsingConnection(connectionString, scope => result = Create(scope).ExecuteNonQuery(queryOptions));
      return result;
    }

    public async Task<int> ExecuteNonQueryAsync(string connectionString, QueryOptions queryOptions = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      int result = 0;
      await SqlScope.UsingConnectionAsync(connectionString,
        async scope => result = await Create(scope).ExecuteNonQueryAsync(queryOptions, cancellationToken));
      return result;
    }

    public object ExecuteScalar(string connectionString, QueryOptions queryOptions = null)
    {
      object result = null;
      SqlScope.UsingConnection(connectionString, scope => Create(scope).ExecuteScalar(queryOptions));
      return result;
    }

    public async Task<object> ExecuteScalarAsync(string connectionString, QueryOptions queryOptions = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      object result = null;
      await SqlScope.UsingConnectionAsync(connectionString,
        async scope => result = await Create(scope).ExecuteScalarAsync(queryOptions, cancellationToken));
      return result;
    }

    private AEnumerable<AsyncState<R>, R> CreateAsyncEnumerable<R>(CancellationToken executeReaderCancellationToken,
      Func<IDataReader, ReadInfo<R>> readInfoGetter, ExecutionOptions options,
      string connectionString)
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
            var reader = await Create(new SqlScope(conn)).ExecuteReaderAsync(options?.QueryOptions, executeReaderCancellationToken).ConfigureAwait(false);
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

    public IEnumerable<object[]> ExecuteQuery(string connectionString, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope).ExecuteQuery(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateObjects(r), options, connectionString);
    }

    public IEnumerable<R> ExecuteQuery<R>(string connectionString, ExecutionOptions options = null) where R : new()
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope).ExecuteQuery<R>(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAsync<R>(string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken)) where R : new()
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateByType<R>(r, options?.ReadOptions), options, connectionString);
    }

    public IEnumerable<R> ExecuteQueryAnonymous<R>(R proto, string connectionString, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope).ExecuteQueryAnonymous(proto, options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAnonymousAsync<R>(R proto, string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateAnonymous(r, proto, options?.ReadOptions),
        options, connectionString);
    }

    public IEnumerable ExecuteQueryFirstColumn(string connectionString, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope).ExecuteQueryFirstColumn(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options, connectionString);
    }

    public IEnumerable<R> ExecuteQueryFirstColumn<R>(string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope).ExecuteQueryFirstColumn<R>(options))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryFirstColumnAsync<R>(string connectionString, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn<R>(r), options, connectionString);
    }

    #endregion

    #region Creation

    public static SimpleCommand Compile(string commandText, params object[] parameters)
    {
      return new SimpleCommand(commandText, parameters);
    }

    #endregion
  }
}
