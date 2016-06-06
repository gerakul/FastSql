using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class SqlScope
  {
    public SqlConnection Connection { get; private set; }
    public SqlTransaction Transaction { get; private set; }

    public SqlScope(SqlConnection connection)
    {
      this.Connection = connection;
      this.Transaction = null;
    }

    public SqlScope(SqlTransaction transaction)
    {
      this.Connection = transaction.Connection;
      this.Transaction = transaction;
    }

    internal SqlCommand CreateCommandInternal(string commandText)
    {
      return Transaction == null ? new SqlCommand(commandText, Connection) : new SqlCommand(commandText, Connection, Transaction);
    }

    public static void UsingConnection(string connectionString, Action<SqlScope> action)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        conn.Open();
        action(new SqlScope(conn));
      }
    }

    public static async Task UsingConnectionAsync(string connectionString, Func<SqlScope, Task> action)
    {
      using (SqlConnection conn = new SqlConnection(connectionString))
      {
        await conn.OpenAsync().ConfigureAwait(false);
        await action(new SqlScope(conn)).ConfigureAwait(false);
      }
    }

    public static void UsingTransaction(SqlConnection connection, IsolationLevel isolationLevel, Action<SqlScope> action)
    {
      SqlTransaction tran = null;
      try
      {
        tran = connection.BeginTransaction(isolationLevel);
        action(new SqlScope(tran));
        tran.Commit();
      }
      catch
      {
        if (tran != null)
        {
          tran.Rollback();
        }

        throw;
      }
    }

    public static void UsingTransaction(SqlConnection connection, Action<SqlScope> action)
    {
      UsingTransaction(connection, IsolationLevel.Unspecified, action);
    }

    public static async Task UsingTransactionAsync(SqlConnection connection, IsolationLevel isolationLevel, Func<SqlScope, Task> action)
    {
      SqlTransaction tran = null;
      try
      {
        tran = connection.BeginTransaction(isolationLevel);
        await action(new SqlScope(tran)).ConfigureAwait(false);
        tran.Commit();
      }
      catch
      {
        if (tran != null)
        {
          tran.Rollback();
        }

        throw;
      }
    }

    public static Task UsingTransactionAsync(SqlConnection connection, Func<SqlScope, Task> action)
    {
      return UsingTransactionAsync(connection, IsolationLevel.Unspecified, action);
    }

    public static void UsingTransaction(string connectionString, IsolationLevel isolationLevel, Action<SqlScope> action)
    {
      UsingConnection(connectionString, scope => UsingTransaction(scope.Connection, isolationLevel, action));
    }

    public static void UsingTransaction(string connectionString, Action<SqlScope> action)
    {
      UsingTransaction(connectionString, IsolationLevel.Unspecified, action);
    }

    public static Task UsingTransactionAsync(string connectionString, IsolationLevel isolationLevel, Func<SqlScope, Task> action)
    {
      return UsingConnectionAsync(connectionString, scope => UsingTransactionAsync(scope.Connection, action));
    }

    public static Task UsingTransactionAsync(string connectionString, Func<SqlScope, Task> action)
    {
      return UsingTransactionAsync(connectionString, IsolationLevel.Unspecified, action);
    }
  }
}
