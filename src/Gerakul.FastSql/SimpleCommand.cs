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

        private SimpleCommand(string commandText)
        {
            this.CommandText = commandText;
        }

        internal DbCommand Create(IDbScope scope, QueryOptions queryOptions, object[] parameters)
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

        #region Creation

        internal static SimpleCommand Compile(string commandText)
        {
            return new SimpleCommand(commandText);
        }

        #endregion

        #region Execution

        public static int ExecuteNonQuery(QueryOptions queryOptions, string connectionString, string commandText, params object[] parameters)
        {
            int result = 0;
            SqlScope.UsingConnection(connectionString, scope =>
            {
                result = scope.CreateSimple(queryOptions, commandText, parameters).ExecuteNonQuery();
            });

            return result;
        }

        public static int ExecuteNonQuery(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteNonQuery(null, connectionString, commandText, parameters);
        }

        public static async Task<int> ExecuteNonQueryAsync(QueryOptions queryOptions, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            int result = 0;
            await SqlScope.UsingConnectionAsync(connectionString, async scope =>
            {
                result = await scope.CreateSimple(queryOptions, commandText, parameters).ExecuteNonQueryAsync(cancellationToken);
            });

            return result;
        }

        public static Task<int> ExecuteNonQueryAsync(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteNonQueryAsync(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        public static object ExecuteScalar(QueryOptions queryOptions, string connectionString, string commandText, params object[] parameters)
        {
            object result = null;
            SqlScope.UsingConnection(connectionString, scope =>
            {
                result = scope.CreateSimple(queryOptions, commandText, parameters).ExecuteScalar();
            });

            return result;
        }

        public static object ExecuteScalar(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteScalar(null, connectionString, commandText, parameters);
        }

        public static async Task<object> ExecuteScalarAsync(QueryOptions queryOptions, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            object result = null;
            await SqlScope.UsingConnectionAsync(connectionString, async scope =>
            {
                result = await scope.CreateSimple(queryOptions, commandText, parameters).ExecuteScalarAsync(cancellationToken);
            });

            return result;
        }

        public static Task<object> ExecuteScalarAsync(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteScalarAsync(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(CancellationToken cancellationToken,
          Func<IDataReader, ReadInfo<T>> readInfoGetter, QueryOptions queryOptions,
          string connectionString, string commandText, object[] parameters)
        {
            return Helpers.CreateAsyncEnumerable<T>(
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
                      SqlScope scope = new SqlScope(conn);
                      var reader = await scope.CreateSimple(queryOptions, commandText, parameters).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        public static IEnumerable<object[]> ExecuteQuery(ExecutionOptions options, string connectionString, string commandText, params object[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlScope scope = new SqlScope(conn);

                foreach (var item in scope.CreateSimple(options?.QueryOptions, commandText, parameters).ExecuteQuery())
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<object[]> ExecuteQuery(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQuery(null, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<object[]> ExecuteQueryAsync(ExecutionOptions options, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateObjects(r), options?.QueryOptions, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<object[]> ExecuteQueryAsync(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryAsync(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        public static IEnumerable<T> ExecuteQuery<T>(ExecutionOptions options, string connectionString, string commandText, params object[] parameters) where T : new()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlScope scope = new SqlScope(conn);

                foreach (var item in scope.CreateSimple(options?.QueryOptions, commandText, parameters).ExecuteQuery<T>(options?.ReadOptions))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ExecuteQuery<T>(string connectionString, string commandText, params object[] parameters) where T : new()
        {
            return ExecuteQuery<T>(null, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(ExecutionOptions options, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters) where T : new()
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateByType<T>(r, options?.ReadOptions), options?.QueryOptions,
              connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryAsync<T>(string connectionString, string commandText, params object[] parameters) where T : new()
        {
            return ExecuteQueryAsync<T>(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        public static IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ExecutionOptions options, string connectionString, string commandText, params object[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlScope scope = new SqlScope(conn);
                foreach (var item in scope.CreateSimple(options?.QueryOptions, commandText, parameters).ExecuteQueryAnonymous(proto, options?.ReadOptions))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryAnonymous(proto, null, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ExecutionOptions options, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateAnonymous(r, proto, options?.ReadOptions),
              options?.QueryOptions, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryAnonymousAsync(proto, null, default(CancellationToken), connectionString, commandText, parameters);
        }

        public static IEnumerable ExecuteQueryFirstColumn(ExecutionOptions options, string connectionString, string commandText, params object[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlScope scope = new SqlScope(conn);
                foreach (var item in scope.CreateSimple(options?.QueryOptions, commandText, parameters).ExecuteQueryFirstColumn())
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable ExecuteQueryFirstColumn(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryFirstColumn(null, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(ExecutionOptions options, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn(r), options?.QueryOptions, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryFirstColumnAsync(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        public static IEnumerable<T> ExecuteQueryFirstColumn<T>(ExecutionOptions options, string connectionString, string commandText, params object[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlScope scope = new SqlScope(conn);
                foreach (var item in scope.CreateSimple(options?.QueryOptions, commandText, parameters).ExecuteQueryFirstColumn<T>())
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ExecuteQueryFirstColumn<T>(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryFirstColumn<T>(null, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(ExecutionOptions options, CancellationToken cancellationToken,
          string connectionString, string commandText, params object[] parameters)
        {
            return CreateAsyncEnumerable(cancellationToken, r => ReadInfoFactory.CreateFirstColumn<T>(r), options?.QueryOptions, connectionString, commandText, parameters);
        }

        public static IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(string connectionString, string commandText, params object[] parameters)
        {
            return ExecuteQueryFirstColumnAsync<T>(null, default(CancellationToken), connectionString, commandText, parameters);
        }

        #endregion
    }
}
