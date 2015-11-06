using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Gerakul.FastSql
{
  public static class EnumerableExtensions
  {
    #region ToDataReader

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, params FieldSettings<T>[] settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings)
    {
      return new UniversalDataReader<T>(values, settings);
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, FromTypeOption option)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(option));
    }

    public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values)
    {
      return new UniversalDataReader<T>(values, FieldSettings.FromType<T>());
    }

    #endregion

    #region WriteToServer

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, connection, destinationTable, fields);
      }
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, transaction, destinationTable, fields);
      }
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(bulkOptions, connectionString, destinationTable, fields);
      }
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, SqlConnection connection, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(connection, destinationTable, fields);
      }
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(transaction, destinationTable, fields);
      }
    }

    public static void WriteToServer<T>(this IEnumerable<T> values, string connectionString, string destinationTable, params string[] fields)
    {
      using (IDataReader reader = values.ToDataReader())
      {
        reader.WriteToServer(connectionString, destinationTable, fields);
      }
    }

    #endregion
  }
}
