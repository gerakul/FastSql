using System;
using System.Collections.Generic;
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

    internal PrecompiledCommand(string commandText, IList<SqlParameter> parameters, IList<FieldSettings<T>> settings)
    {
      this.CommandText = commandText;

      if (parameters == null || parameters.Count == 0)
      {
        Map = new ParMap<T>[0];
      }

      if (parameters.GroupBy(x => x.ParameterName.GetSqlParameterName().ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Parameter names has been duplicated");
      }

      if (settings.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Setting names has been duplicated");
      }

      Map = parameters
        .Join(settings,
          x => x.ParameterName.GetSqlParameterName().ToLowerInvariant(),
          x => x.Name.ToLowerInvariant(),
          (x, y) => new ParMap<T>() { Parameter = x, Settings = y }).ToArray();

      if (Map.Length < parameters.Count)
      {
        throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
      }
    }

    public Command<T> Create(SqlConnection conn, QueryOptions queryOptions = null)
    {
      return new Command<T>(this, conn, queryOptions);
    }

    public Command<T> Create(SqlTransaction tran, QueryOptions queryOptions = null)
    {
      return new Command<T>(this, tran, queryOptions);
    }

    public Command<T> Create(SqlExecutor executor, QueryOptions queryOptions = null)
    {
      return new Command<T>(this, executor, queryOptions);
    }

    public int ExecuteNonQuery(string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        return Create(conn, queryOptions).ExecuteNonQuery(value);
      }
    }

    public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken, string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync();
        return await Create(conn, queryOptions).ExecuteNonQueryAsync(cancellationToken, value);
      }
    }

    public Task<int> ExecuteNonQueryAsync(string connectionString, T value, QueryOptions queryOptions = null)
    {
      return ExecuteNonQueryAsync(CancellationToken.None, connectionString, value, queryOptions);
    }

    public object ExecuteScalar(string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        return Create(conn, queryOptions).ExecuteScalar(value);
      }
    }

    public async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken, string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync();
        return await Create(conn, queryOptions).ExecuteScalarAsync(cancellationToken, value);
      }
    }

    public Task<object> ExecuteScalarAsync(string connectionString, T value, QueryOptions queryOptions = null)
    {
      return ExecuteScalarAsync(CancellationToken.None, connectionString, value, queryOptions);
    }

    public SqlDataReader ExecuteReader(string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        return Create(conn, queryOptions).ExecuteReader(value);
      }
    }

    public async Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken, string connectionString, T value, QueryOptions queryOptions = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync();
        return await Create(conn, queryOptions).ExecuteReaderAsync(cancellationToken, value);
      }
    }

    public Task<SqlDataReader> ExecuteReaderAsync(string connectionString, T value, QueryOptions queryOptions = null)
    {
      return ExecuteReaderAsync(CancellationToken.None, connectionString, value, queryOptions);
    }
  }

  internal class ParMap<T>
  {
    public SqlParameter Parameter;
    public FieldSettings<T> Settings;
  }
}
