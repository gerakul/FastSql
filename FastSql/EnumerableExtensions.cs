using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public static class EnumerableExtensions
  {
    #region ToDataReader

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, params FieldSettings<T>[] settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, params FieldSettings<T>[] settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, FromTypeOption fromTypeOption)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(fromTypeOption));
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, FromTypeOption fromTypeOption)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(fromTypeOption));
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>());
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>());
    }

    #endregion

    #region WriteToServer

    // ::: возможно тут надо реализовывать DbDataReader чтобы добиться истинной асинхронности при записи данных на сервер
    // ::: т.к. BulkCopy будет использовать метод Read() а не ReadAsync() исходного ридера, если он не DbDataReader

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, connection, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken, 
      BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, bulkOptions, connection, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, bulkOptions, connection, destinationTable, fields);
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, transaction, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, bulkOptions, transaction, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, bulkOptions, transaction, destinationTable, fields);
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, connectionString, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken, 
      BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, bulkOptions, connectionString, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, bulkOptions, connectionString, destinationTable, fields);
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(connection, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken,
      SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, connection, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, SqlConnection connection, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, connection, destinationTable, fields);
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(transaction, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken,
      SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, transaction, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, transaction, destinationTable, fields);
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(connectionString, destinationTable, fields);
      }
    }

    public static async Task WriteToServerAsync<T>(this IEnumerable<T> values, CancellationToken cancellationToken,
      string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        await reader.WriteToServerAsync(cancellationToken, connectionString, destinationTable, fields).ConfigureAwait(false);
      }
    }

    public static Task WriteToServerAsync<T>(this IEnumerable<T> values, string connectionString, string destinationTable, params string[] fields)
    {
      return values.WriteToServerAsync(CancellationToken.None, connectionString, destinationTable, fields);
    }

    #endregion
  }
}
