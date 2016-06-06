using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public static class DbCommandExtensions
  {
    public static int ExecuteNonQuery(this DbCommand cmd, QueryOptions queryOptions)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteNonQuery();
    }

    public static Task<int> ExecuteNonQueryAsync(this DbCommand cmd, QueryOptions queryOptions, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public static object ExecuteScalar(this DbCommand cmd, QueryOptions queryOptions)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteScalar();
    }

    public static Task<object> ExecuteScalarAsync(this DbCommand cmd, QueryOptions queryOptions, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteScalarAsync(cancellationToken);
    }

    public static DbDataReader ExecuteReader(this DbCommand cmd, QueryOptions queryOptions)
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteReader();
    }

    public static Task<DbDataReader> ExecuteReaderAsync(this DbCommand cmd, QueryOptions queryOptions, CancellationToken cancellationToken = default(CancellationToken))
    {
      Helpers.ApplyQueryOptions(cmd, queryOptions);
      return cmd.ExecuteReaderAsync(cancellationToken);
    }

    private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(DbCommand cmd, CancellationToken executeReaderCancellationToken,
      Func<IDataReader, ReadInfo<T>> readInfoGetter, ExecutionOptions options)
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
          var reader = await cmd.ExecuteReaderAsync(options?.QueryOptions, executeReaderCancellationToken).ConfigureAwait(false);
          state.ReadInfo = readInfoGetter(reader);
        });
    }

    public static IEnumerable<object[]> ExecuteQuery(this DbCommand cmd, ExecutionOptions options = null)
    {
      using (DbDataReader reader = cmd.ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAll())
        {
          yield return item;
        }
      }
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync(this DbCommand cmd, ExecutionOptions options = null, CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cmd, executeReaderCancellationToken, r => ReadInfoFactory.CreateObjects(r), options);
    }

    public static IEnumerable<T> ExecuteQuery<T>(this DbCommand cmd, ExecutionOptions options = null) where T : new()
    {
      using (DbDataReader reader = cmd.ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAll<T>(options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(this DbCommand cmd, ExecutionOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken)) where T : new()
    {
      return CreateAsyncEnumerable(cmd, executeReaderCancellationToken, r => ReadInfoFactory.CreateByType<T>(r, options?.ReadOptions), options);
    }

    public static IEnumerable<T> ExecuteQueryAnonymous<T>(this DbCommand cmd, T proto, ExecutionOptions options = null)
    {
      using (DbDataReader reader = cmd.ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllAnonymous(proto, options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(this DbCommand cmd, T proto, ExecutionOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cmd, executeReaderCancellationToken, r => ReadInfoFactory.CreateAnonymous<T>(r, proto, options?.ReadOptions), 
        options);
    }

    public static IEnumerable ExecuteQueryFirstColumn(this DbCommand cmd, ExecutionOptions options = null)
    {
      using (DbDataReader reader = cmd.ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllFirstColumn())
        {
          yield return item;
        }
      }
    }

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(this DbCommand cmd, ExecutionOptions options = null, 
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cmd, executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options);
    }

    public static IEnumerable<T> ExecuteQueryFirstColumn<T>(this DbCommand cmd, ExecutionOptions options = null)
    {
      using (DbDataReader reader = cmd.ExecuteReader(options?.QueryOptions))
      {
        foreach (var item in reader.ReadAllFirstColumn<T>())
        {
          yield return item;
        }
      }
    }

    public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(this DbCommand cmd, ExecutionOptions options = null,
      CancellationToken executeReaderCancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cmd, executeReaderCancellationToken, r => ReadInfoFactory.CreateFirstColumn<T>(r), options);
    }
  }
}
