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
      int result = 0;
      Commands.UsingConnection(connectionString, conn => result = Create(conn, queryOptions).Fill(value).ExecuteNonQuery());
      return result;
    }

    public async Task<int> ExecuteNonQueryAsync(string connectionString, T value, QueryOptions queryOptions = null, 
      CancellationToken cancellationToken = default(CancellationToken))
    {
      int result = 0;
      await Commands.UsingConnectionAsync(connectionString, 
        async conn => result = await Create(conn, queryOptions).Fill(value).ExecuteNonQueryAsync(null, cancellationToken));
      return result;
    }

    public object ExecuteScalar(string connectionString, T value, QueryOptions queryOptions = null)
    {
      object result = null;
      Commands.UsingConnection(connectionString, conn => Create(conn, queryOptions).Fill(value).ExecuteScalar());
      return result;
    }

    public async Task<object> ExecuteScalarAsync(string connectionString, T value, QueryOptions queryOptions = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      object result = null;
      await Commands.UsingConnectionAsync(connectionString,
        async conn => result = await Create(conn, queryOptions).Fill(value).ExecuteScalarAsync(null, cancellationToken));
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
            var reader = await Create(conn, options?.QueryOptions).Fill(value).ExecuteReaderAsync(null, executeReaderCancellationToken).ConfigureAwait(false);
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
        foreach (var item in Create(conn).Fill(value).ExecuteQuery(options))
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
        foreach (var item in Create(conn).Fill(value).ExecuteQuery<R>(options))
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
        foreach (var item in Create(conn).Fill(value).ExecuteQueryAnonymous(proto, options))
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
        foreach (var item in Create(conn).Fill(value).ExecuteQueryFirstColumn(options))
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
        foreach (var item in Create(conn).Fill(value).ExecuteQueryFirstColumn<R>(options))
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
    public SqlParameter Parameter;
    public FieldSettings<T> Settings;

    public ParMap<T> Clone()
    {
      return new ParMap<T>()
      {
        Parameter = new SqlParameter(
          Parameter.ParameterName,
          Parameter.SqlDbType,
          Parameter.Size,
          Parameter.Direction,
          Parameter.Precision,
          Parameter.Scale,
          Parameter.SourceColumn,
          Parameter.SourceVersion,
          Parameter.SourceColumnNullMapping,
          Parameter.Value,
          Parameter.XmlSchemaCollectionDatabase,
          Parameter.XmlSchemaCollectionOwningSchema,
          Parameter.XmlSchemaCollectionName)
        {
          CompareInfo = Parameter.CompareInfo,
          IsNullable = Parameter.IsNullable,
          LocaleId = Parameter.LocaleId,
          TypeName = Parameter.TypeName,
          UdtTypeName = Parameter.UdtTypeName,
          SqlValue = Parameter.SqlValue,
          Offset = Parameter.Offset
        },

        Settings = Settings
      };
    }
  }
}
