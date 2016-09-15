using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class MappedCommand<T>
  {
    public string CommandText { get; private set; }
    private ParMap<T>[] map;

    internal MappedCommand(string commandText, IList<string> parameters, IList<FieldSettings<T>> settings)
    {
      this.CommandText = commandText;

      if (parameters == null || parameters.Count == 0)
      {
        map = new ParMap<T>[0];
      }

      if (parameters.GroupBy(x => x.GetSqlParameterName().ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Parameter names has been duplicated");
      }

      if (settings.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
      {
        throw new ArgumentException("Setting names has been duplicated");
      }

      map = parameters
        .Join(settings,
          x => x.GetSqlParameterName().ToLowerInvariant(),
          x => x.Name.ToLowerInvariant(),
          (x, y) => new ParMap<T>() { ParameterName = x, Settings = y }).ToArray();

      if (map.Length < parameters.Count)
      {
        throw new ArgumentException("Not all parameters has been mapped on settings", nameof(parameters));
      }
    }

    internal DbCommand Create(IDbScope scope, T value, QueryOptions queryOptions = null)
    {
      var cmd = scope.CreateCommand(CommandText);

      for (int i = 0; i < map.Length; i++)
      {
        var m = map[i];
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

        scope.AddParamWithValue(cmd, m.ParameterName, paramValue);
      }

      Helpers.ApplyQueryOptions(cmd, queryOptions ?? new QueryOptions());

      return cmd;
    }

    #region Execution

    public int ExecuteNonQuery(string connectionString, T value, QueryOptions queryOptions = null)
    {
      int result = 0;
      SqlScope.UsingConnection(connectionString, scope =>
      {
        result = Create(scope, value, queryOptions).ExecuteNonQuery();
      });

      return result;
    }

    public async Task<int> ExecuteNonQueryAsync(string connectionString, T value, QueryOptions queryOptions = null, 
      CancellationToken cancellationToken = default(CancellationToken))
    {
      int result = 0;
      await SqlScope.UsingConnectionAsync(connectionString, async scope =>
      {
        result = await Create(scope, value, queryOptions).ExecuteNonQueryAsync(cancellationToken);
      });

      return result;
    }

    public object ExecuteScalar(string connectionString, T value, QueryOptions queryOptions = null)
    {
      object result = 0;
      SqlScope.UsingConnection(connectionString, scope =>
      {
        result = Create(scope, value, queryOptions).ExecuteScalar();
      });

      return result;
    }

    public async Task<object> ExecuteScalarAsync(string connectionString, T value, QueryOptions queryOptions = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      object result = 0;
      await SqlScope.UsingConnectionAsync(connectionString, async scope =>
      {
        result = await Create(scope, value, queryOptions).ExecuteScalarAsync(cancellationToken);
      });

      return result;
    }

    private AEnumerable<AsyncState<R>, R> CreateAsyncEnumerable<R>(CancellationToken cancellationToken,
      Func<IDataReader, ReadInfo<R>> readInfoGetter, QueryOptions queryOptions,
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
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            state.InternalConnection = conn;
            var reader = await Create(new SqlScope(conn), value, queryOptions).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    public IEnumerable<object[]> ExecuteQuery(string connectionString, T value, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value, options?.QueryOptions).ExecuteQuery())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object[]> ExecuteQueryAsync(string connectionString, T value, ExecutionOptions options = null, 
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateObjects(r), options?.QueryOptions, connectionString, value);
    }

    public IEnumerable<R> ExecuteQuery<R>(string connectionString, T value, ExecutionOptions options = null) where R : new()
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value, options?.QueryOptions).ExecuteQuery<R>(options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAsync<R>(string connectionString, T value, ExecutionOptions options = null,
      CancellationToken cancellationToken = default(CancellationToken)) where R : new()
    {
      return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateByType<R>(r, options?.ReadOptions), options?.QueryOptions, connectionString, value);
    }

    public IEnumerable<R> ExecuteQueryAnonymous<R>(R proto, string connectionString, T value, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value, options?.QueryOptions).ExecuteQueryAnonymous(proto, options?.ReadOptions))
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryAnonymousAsync<R>(R proto, string connectionString, T value, ExecutionOptions options = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateAnonymous(r, proto, options?.ReadOptions),
        options?.QueryOptions, connectionString, value);
    }

    public IEnumerable ExecuteQueryFirstColumn(string connectionString, T value, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value, options?.QueryOptions).ExecuteQueryFirstColumn())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string connectionString, T value, ExecutionOptions options = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options?.QueryOptions, connectionString, value);
    }

    public IEnumerable<R> ExecuteQueryFirstColumn<R>(string connectionString, T value, ExecutionOptions options = null)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        SqlScope scope = new SqlScope(conn);
        foreach (var item in Create(scope, value, options?.QueryOptions).ExecuteQueryFirstColumn<R>())
        {
          yield return item;
        }
      }
    }

    public IAsyncEnumerable<R> ExecuteQueryFirstColumnAsync<R>(string connectionString, T value, ExecutionOptions options = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn<R>(r), options?.QueryOptions, connectionString, value);
    }

    #endregion
  }

  public static class MappedCommand
  {
    #region Creation

    private static Regex regName = new Regex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline);

    private static string[] ParseCommandText(string commandText)
    {
      return regName.Matches(commandText).Cast<Match>()
        .Select(x => "@" + x.Groups["Name"].ToString().ToLowerInvariant())
        .Distinct().ToArray();
    }

    public static MappedCommand<T> Compile<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings)
    {
      return new MappedCommand<T>(commandText, paramNames, settings);
    }

    public static MappedCommand<T> Compile<T>(string commandText, IList<FieldSettings<T>> settings)
    {
      return new MappedCommand<T>(commandText, ParseCommandText(commandText), settings);
    }

    public static MappedCommand<T> Compile<T>(string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new MappedCommand<T>(commandText, paramNames, FieldSettings.FromType<T>(fromTypeOption));
    }

    public static MappedCommand<T> Compile<T>(string commandText, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new MappedCommand<T>(commandText, ParseCommandText(commandText), FieldSettings.FromType<T>(fromTypeOption));
    }

    public static MappedCommand<T> Compile<T>(T proto, string commandText, IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new MappedCommand<T>(commandText, paramNames, FieldSettings.FromType(proto, fromTypeOption));
    }

    public static MappedCommand<T> Compile<T>(T proto, string commandText, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new MappedCommand<T>(commandText, ParseCommandText(commandText), FieldSettings.FromType(proto, fromTypeOption));
    }

    #region Preparation

    public static PreparedMappedCommand<T> Prepare<T>(string connectionString, string commandText, T value, 
      IList<string> paramNames, IList<FieldSettings<T>> settings)
    {
      return new PreparedMappedCommand<T>(Compile(commandText, paramNames, settings), connectionString, value);
    }

    public static PreparedMappedCommand<T> Prepare<T>(string connectionString, string commandText, T value, 
      IList<FieldSettings<T>> settings)
    {
      return new PreparedMappedCommand<T>(Compile(commandText, settings), connectionString, value);
    }

    public static PreparedMappedCommand<T> Prepare<T>(string connectionString, string commandText, T value, 
      IList<string> paramNames, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new PreparedMappedCommand<T>(Compile<T>(commandText, paramNames, fromTypeOption), connectionString, value);
    }

    public static PreparedMappedCommand<T> Prepare<T>(string connectionString, string commandText, T value, 
      FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      return new PreparedMappedCommand<T>(Compile<T>(commandText, fromTypeOption), connectionString, value);
    }

    #endregion

    #region Special commands

    #region Insert

    public static MappedCommand<T> CompileInsert<T>(string tableName, IList<FieldSettings<T>> settings, bool getIdentity, params string[] ignoreFields)
    {
      var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(ignoreFields.Select(x => x.ToLowerInvariant())).ToArray();
      string query = CommandTextGenerator.Insert(tableName, getIdentity, fields);
      return new MappedCommand<T>(query, fields, settings);
    }

    public static MappedCommand<T> CompileInsert<T>(string tableName, FromTypeOption fromTypeOption, bool getIdentity, params string[] ignoreFields)
    {
      return CompileInsert(tableName, FieldSettings.FromType<T>(fromTypeOption), getIdentity, ignoreFields);
    }

    public static MappedCommand<T> CompileInsert<T>(string tableName, bool getIdentity, params string[] ignoreFields)
    {
      return CompileInsert(tableName, FieldSettings.FromType<T>(FromTypeOption.Both), getIdentity, ignoreFields);
    }

    public static MappedCommand<T> CompileInsert<T>(T proto, string tableName, FromTypeOption fromTypeOption, bool getIdentity, params string[] ignoreFields)
    {
      return CompileInsert(tableName, FieldSettings.FromType(proto, fromTypeOption), getIdentity, ignoreFields);
    }

    public static MappedCommand<T> CompileInsert<T>(T proto, string tableName, bool getIdentity, params string[] ignoreFields)
    {
      return CompileInsert(tableName, FieldSettings.FromType(proto, FromTypeOption.Both), getIdentity, ignoreFields);
    }

    #endregion

    #region Update

    public static MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, IList<string> keyFields, IList<string> notKeyIgnoreFields)
    {
      var fields = settings.Select(x => x.Name.ToLowerInvariant()).Except(notKeyIgnoreFields.Select(x => x.ToLowerInvariant())).ToArray();
      var fieldsToUpdate = fields.Except(keyFields.Select(x => x.ToLowerInvariant())).ToArray();
      string query = CommandTextGenerator.Update(tableName, keyFields, fieldsToUpdate);
      return new MappedCommand<T>(query, fields, settings);
    }

    public static MappedCommand<T> CompileUpdate<T>(string tableName, IList<FieldSettings<T>> settings, params string[] keyFields)
    {
      return CompileUpdate(tableName, settings, keyFields, new List<string>());
    }

    public static MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, notKeyIgnoreFields);
    }

    public static MappedCommand<T> CompileUpdate<T>(string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType<T>(fromTypeOption), keyFields, new List<string>());
    }

    public static MappedCommand<T> CompileUpdate<T>(string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, notKeyIgnoreFields);
    }

    public static MappedCommand<T> CompileUpdate<T>(string tableName, params string[] keyFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType<T>(), keyFields, new List<string>());
    }

    public static MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, IList<string> keyFields, IList<string> notKeyIgnoreFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, notKeyIgnoreFields);
    }

    public static MappedCommand<T> CompileUpdate<T>(T proto, string tableName, FromTypeOption fromTypeOption, params string[] keyFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType(proto, fromTypeOption), keyFields, new List<string>());
    }

    public static MappedCommand<T> CompileUpdate<T>(T proto, string tableName, IList<string> keyFields, IList<string> notKeyIgnoreFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, notKeyIgnoreFields);
    }

    public static MappedCommand<T> CompileUpdate<T>(T proto, string tableName, params string[] keyFields)
    {
      return CompileUpdate(tableName, FieldSettings.FromType(proto), keyFields, new List<string>());
    }

    #endregion

    #endregion

    #endregion

    #region Execution

    public static int ExecuteNonQuery<T>(string connectionString, string commandText, T value,
      QueryOptions queryOptions = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteNonQuery(connectionString, value, queryOptions);
    }

    public static Task<int> ExecuteNonQueryAsync<T>(string connectionString, string commandText, T value,
      QueryOptions queryOptions = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteNonQueryAsync(connectionString, value, queryOptions, cancellationToken);
    }

    public static object ExecuteScalar<T>(string connectionString, string commandText, T value,
      QueryOptions queryOptions = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteScalar(connectionString, value, queryOptions);
    }

    public static Task<object> ExecuteScalarAsync<T>(string connectionString, string commandText, T value,
      QueryOptions queryOptions = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteScalarAsync(connectionString, value, queryOptions, cancellationToken);
    }

    public static IEnumerable<object[]> ExecuteQuery<T>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteQuery(connectionString, value, options);
    }

    public static IAsyncEnumerable<object[]> ExecuteQueryAsync<T>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryAsync(connectionString, value, options, cancellationToken);
    }

    public static IEnumerable<R> ExecuteQuery<T, R>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both) where R : new()
    {
      return Compile<T>(commandText, typeOption).ExecuteQuery<R>(connectionString, value, options);
    }

    public static IAsyncEnumerable<R> ExecuteQueryAsync<T, R>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken)) where R : new()
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryAsync<R>(connectionString, value, options, cancellationToken);
    }

    public static IEnumerable<R> ExecuteQueryAnonymous<T, R>(R proto, string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryAnonymous(proto, connectionString, value, options);
    }

    public static IAsyncEnumerable<R> ExecuteQueryAnonymousAsync<T, R>(R proto, string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryAnonymousAsync(proto, connectionString, value, options, cancellationToken);
    }

    public static IEnumerable ExecuteQueryFirstColumn<T>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryFirstColumn(connectionString, value, options);
    }

    public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync<T>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryFirstColumnAsync(connectionString, value, options, cancellationToken);
    }

    public static IEnumerable<R> ExecuteQueryFirstColumn<T, R>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both)
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryFirstColumn<R>(connectionString, value, options);
    }

    public static IAsyncEnumerable<R> ExecuteQueryFirstColumnAsync<T, R>(string connectionString, string commandText, T value,
      ExecutionOptions options = null, FromTypeOption typeOption = FromTypeOption.Both, CancellationToken cancellationToken = default(CancellationToken))
    {
      return Compile<T>(commandText, typeOption).ExecuteQueryFirstColumnAsync<R>(connectionString, value, options, cancellationToken);
    }

    #region Special commands

    #region Insert

    public static int Insert<T>(QueryOptions queryOptions, string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return CompileInsert<T>(tableName, false, ignoreFields).ExecuteNonQuery(connectionString, value, queryOptions);
    }

    public static int Insert<T>(string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return CompileInsert<T>(tableName, false, ignoreFields).ExecuteNonQuery(connectionString, value);
    }

    public static Task<int> InsertAsync<T>(QueryOptions queryOptions, CancellationToken cancellationToken,
      string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return CompileInsert<T>(tableName, false, ignoreFields).ExecuteNonQueryAsync(connectionString, value, queryOptions, cancellationToken);
    }

    public static Task<int> InsertAsync<T>(string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return CompileInsert<T>(tableName, false, ignoreFields).ExecuteNonQueryAsync(connectionString, value);
    }

    #endregion

    #region Insert and get identity

    public static decimal InsertAndGetId<T>(QueryOptions queryOptions, string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return (decimal)CompileInsert<T>(tableName, true, ignoreFields).ExecuteScalar(connectionString, value, queryOptions);
    }

    public static decimal InsertAndGetId<T>(string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return (decimal)CompileInsert<T>(tableName, true, ignoreFields).ExecuteScalar(connectionString, value);
    }

    public static async Task<decimal> InsertAndGetIdAsync<T>(QueryOptions queryOptions, CancellationToken cancellationToken,
      string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return (decimal)(await CompileInsert<T>(tableName, true, ignoreFields).ExecuteScalarAsync(connectionString, value, queryOptions, cancellationToken));
    }

    public static async Task<decimal> InsertAndGetIdAsync<T>(string connectionString, string tableName, T value, params string[] ignoreFields)
    {
      return (decimal)(await CompileInsert<T>(tableName, true, ignoreFields).ExecuteScalarAsync(connectionString, value));
    }

    #endregion

    #region Update

    public static int Update<T>(QueryOptions queryOptions, string connectionString, string tableName, T value, params string[] keyFields)
    {
      return CompileUpdate<T>(tableName, keyFields).ExecuteNonQuery(connectionString, value, queryOptions);
    }

    public static int Update<T>(string connectionString, string tableName, T value, params string[] keyFields)
    {
      return CompileUpdate<T>(tableName, keyFields).ExecuteNonQuery(connectionString, value);
    }

    public static Task<int> UpdateAsync<T>(QueryOptions queryOptions, CancellationToken cancellationToken, 
      string connectionString, string tableName, T value, params string[] keyFields)
    {
      return CompileUpdate<T>(tableName, keyFields).ExecuteNonQueryAsync(connectionString, value, queryOptions, cancellationToken);
    }

    public static Task<int> UpdateAsync<T>(string connectionString, string tableName, T value, params string[] keyFields)
    {
      return CompileUpdate<T>(tableName, keyFields).ExecuteNonQueryAsync(connectionString, value);
    }

    #endregion

    #endregion

    #endregion
  }

  internal class ParMap<T>
  {
    public string ParameterName;
    public FieldSettings<T> Settings;
  }
}
