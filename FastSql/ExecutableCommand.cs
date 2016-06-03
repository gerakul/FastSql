using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class ExecutableCommand
  {
    private SqlCommand cmd;

    public ExecutableCommand(SqlCommand cmd)
    {
      this.cmd = cmd;
    }

    public int ExecuteNonQuery(QueryOptions queryOptions = null)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteNonQuery();
    }

    public Task<int> ExecuteNonQueryAsync(QueryOptions queryOptions = null, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public object ExecuteScalar(QueryOptions queryOptions = null)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteScalar();
    }

    public Task<object> ExecuteScalarAsync(QueryOptions queryOptions = null, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteScalarAsync(cancellationToken);
    }

    public SqlDataReader ExecuteReader(QueryOptions queryOptions = null)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteReader();
    }

    public Task<SqlDataReader> ExecuteReaderAsync(QueryOptions queryOptions = null, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteReaderAsync(cancellationToken);
    }

    private AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(CancellationToken executeReaderCancellationToken,
      Func<IDataReader, ReadInfo<T>> readInfoGetter, SqlExecutorOptions options)
    {
      return Helpers.CreateAsyncEnumerable<T>(
        state =>
        {
          if (state.ReadInfo != null)
          {
            state.ReadInfo.Reader.Close();
          }
        },

        async (state, ct) =>
        {
          var reader = await ExecuteReaderAsync(options?.QueryOptions, executeReaderCancellationToken).ConfigureAwait(false);
          state.ReadInfo = readInfoGetter(reader);
        });
    }

    public IEnumerable<object[]> ExecuteQuery(SqlExecutorOptions options = null)
    {
      using (SqlDataReader reader = ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAll())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(SqlExecutorOptions options = null, CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateObjects(r), options);
    }

    public IEnumerable<T> ExecuteQuery<T>(SqlExecutorOptions options = null) where T : new()
    {
      using (SqlDataReader reader = ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAll<T>(options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<T> ExecuteQueryAsync<T>(SqlExecutorOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken)) where T : new()
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateByType<T>(r, options?.ReadOptions), options);
    }

    public IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, SqlExecutorOptions options = null)
    {
      using (SqlDataReader reader = ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllAnonymous(proto, options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, SqlExecutorOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, options?.ReadOptions), 
        options);
    }

    public IEnumerable ExecuteQueryFirstColumn(SqlExecutorOptions options = null)
    {
      using (SqlDataReader reader = ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllFirstColumn())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(SqlExecutorOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options);
    }

    public IEnumerable<T> ExecuteQueryFirstColumn<T>(SqlExecutorOptions options = null)
    {
      using (SqlDataReader reader = ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllFirstColumn<T>())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(SqlExecutorOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn<T>(r), options);
    }
  }
}
