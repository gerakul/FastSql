using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class Command<T>
  {
    private SqlCommand cmd;
    private ParMap<T>[] map;

    internal Command(PrecompiledCommand<T> precompiledCommand, SqlConnection conn, QueryOptions queryOptions = null)
    {
      cmd = new SqlCommand(precompiledCommand.CommandText, conn);
      ApplyArguments(precompiledCommand, queryOptions);
    }

    internal Command(PrecompiledCommand<T> precompiledCommand, SqlTransaction tran, QueryOptions queryOptions = null)
    {
      cmd = new SqlCommand(precompiledCommand.CommandText, tran.Connection, tran);
      ApplyArguments(precompiledCommand, queryOptions);
    }

    internal Command(PrecompiledCommand<T> precompiledCommand, SqlExecutor executor, QueryOptions queryOptions = null)
    {
      cmd = executor.CreateCommandInternal(precompiledCommand.CommandText);
      ApplyArguments(precompiledCommand, queryOptions);
    }

    private void ApplyArguments(PrecompiledCommand<T> precompiledCommand, QueryOptions queryOptions)
    {
      map = precompiledCommand.Map.Select(x => x.Clone()).ToArray();
      cmd.Parameters.AddRange(map.Select(x => x.Parameter).ToArray());
      Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());
    }

    public ExecutableCommand Fill(T value)
    {
      foreach (var m in map)
      {
        if (m.Settings.DetermineNullByValue)
        {
          var val = m.Settings.Getter(value);
          m.Parameter.Value = val == null ? DBNull.Value : val;
        }
        else
        {
          m.Parameter.Value = m.Settings.IsNullGetter(value) ? DBNull.Value : m.Settings.Getter(value);
        }
      }

      return new ExecutableCommand(cmd);
    }

    public int ExecuteNonQuery(T value, QueryOptions queryOptions = null)
    {
      return Fill(value).ExecuteNonQuery(queryOptions);
    }

    public Task<int> ExecuteNonQueryAsync(T value, CancellationToken cancellationToken = default(CancellationToken), QueryOptions queryOptions = null)
    {
      return Fill(value).ExecuteNonQueryAsync(queryOptions, cancellationToken);
    }

    public object ExecuteScalar(T value, QueryOptions queryOptions = null)
    {
      return Fill(value).ExecuteScalar(queryOptions);
    }

    public Task<object> ExecuteScalarAsync(T value, CancellationToken cancellationToken = default(CancellationToken), QueryOptions queryOptions = null)
    {
      return Fill(value).ExecuteScalarAsync(queryOptions, cancellationToken);
    }
  }
}
