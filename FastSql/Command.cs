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
    private PrecompiledCommand<T> precompiledCommand;

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
      cmd.Parameters.AddRange(precompiledCommand.Map.Select(x => x.Parameter).ToArray());
      Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());
      this.precompiledCommand = precompiledCommand;
    }

    private void Fill(T value)
    {
      foreach (var m in precompiledCommand.Map)
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
    }

    public int ExecuteNonQuery(T value)
    {
      Fill(value);
      return cmd.ExecuteNonQuery();
    }

    public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken, T value)
    {
      Fill(value);
      return cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<int> ExecuteNonQueryAsync(T value)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, value);
    }

    public object ExecuteScalar(T value)
    {
      Fill(value);
      return cmd.ExecuteScalar();
    }

    public Task<object> ExecuteScalarAsync(CancellationToken cancellationToken, T value)
    {
      Fill(value);
      return cmd.ExecuteScalarAsync(cancellationToken);
    }

    public Task<object> ExecuteScalarAsync(T value)
    {
      return ExecuteScalarAsync(CancellationToken.None, value);
    }

    public SqlDataReader ExecuteReader(T value)
    {
      Fill(value);
      return cmd.ExecuteReader();
    }

    public Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken, T value)
    {
      Fill(value);
      return cmd.ExecuteReaderAsync(cancellationToken);
    }

    public Task<SqlDataReader> ExecuteReaderAsync(T value)
    {
      return ExecuteReaderAsync(CancellationToken.None, value);
    }
  }
}
