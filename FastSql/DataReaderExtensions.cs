using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace FastSql
{
  public static class DataReaderExtensions
  {
    #region Read

    private class ReaderField
    {
      public int Ordinal;
      public string Name;
    }

    public static IEnumerable<object[]> ReadAll(this IDataReader reader)
    {
      int len = reader.FieldCount;
      while (reader.Read())
      {
        object[] objects = new object[len];
        reader.GetValues(objects);
        yield return objects;
      }
    }

    public static IEnumerable<T> ReadAll<T>(this IDataReader reader, ReadOptions readOptions) where T : new()
    {
      ReaderField[] readerFields = new ReaderField[reader.FieldCount];

      for (int i = 0; i < reader.FieldCount; i++)
      {
        readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
      }

      Type type = typeof(T);

      FieldInfo[] fiAll = new FieldInfo[0];
      if ((readOptions.FromTypeOption & FromTypeOption.PublicField) == FromTypeOption.PublicField)
      {
        fiAll = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
      }

      PropertyInfo[] piAll = new PropertyInfo[0];
      if ((readOptions.FromTypeOption & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
      {
        piAll = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
      }

      var fiCommon = readOptions.CaseSensitive ?
        fiAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray() :
        fiAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray();

      var piCommon = readOptions.CaseSensitive ?
        piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray() :
        piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray();

      Helpers.CheckFieldSelection(readOptions.FieldsSelector, readerFields.Length, fiAll.Length + piAll.Length, fiCommon.Length + piCommon.Length);

      Type nullableTypeDef = typeof(Nullable<int>).GetGenericTypeDefinition();

      FieldInfo[] fi = new FieldInfo[fiCommon.Length];
      int[] fiInd = new int[fiCommon.Length];
      bool[] fiNullable = new bool[fiCommon.Length];

      for (int i = 0; i < fiCommon.Length; i++)
      {
        fi[i] = fiCommon[i].FieldInfo;
        fiInd[i] = fiCommon[i].ReaderField.Ordinal;
        fiNullable[i] = !fiCommon[i].FieldInfo.FieldType.IsValueType 
          || (fiCommon[i].FieldInfo.FieldType.IsGenericType && fiCommon[i].FieldInfo.FieldType.GetGenericTypeDefinition().Equals(nullableTypeDef));
      }

      PropertyInfo[] pi = new PropertyInfo[piCommon.Length];
      int[] piInd = new int[piCommon.Length];
      bool[] piNullable = new bool[piCommon.Length];

      for (int i = 0; i < piCommon.Length; i++)
      {
        pi[i] = piCommon[i].PropertyInfo;
        piInd[i] = piCommon[i].ReaderField.Ordinal;
        piNullable[i] = !piCommon[i].PropertyInfo.PropertyType.IsValueType 
          || (piCommon[i].PropertyInfo.PropertyType.IsGenericType && piCommon[i].PropertyInfo.PropertyType.GetGenericTypeDefinition().Equals(nullableTypeDef));
      }

      while (reader.Read())
      {
        T obj = new T();

        for (int i = 0; i < fi.Length; i++)
        {
          fi[i].SetValue(obj, reader.IsDBNull(fiInd[i]) && fiNullable[i] ? null : reader.GetValue(fiInd[i]));
        }

        for (int i = 0; i < pi.Length; i++)
        {
          pi[i].SetValue(obj, reader.IsDBNull(piInd[i]) && piNullable[i] ? null : reader.GetValue(piInd[i]));
        }

        yield return obj;
      }
    }

    public static IEnumerable<T> ReadAll<T>(this IDataReader reader) where T : new()
    {
      return ReadAll<T>(reader, new ReadOptions());
    }

    public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto, ReadOptions readOptions)
    {
      ReaderField[] readerFields = new ReaderField[reader.FieldCount];

      for (int i = 0; i < reader.FieldCount; i++)
      {
        readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
      }

      ConstructorInfo constructor = typeof(T).GetConstructors().First();
      ParameterInfo[] piAll = constructor.GetParameters();

      var piCommon = readOptions.CaseSensitive ?
        piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray() :
        piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray();

      Helpers.CheckFieldSelection(readOptions.FieldsSelector, readerFields.Length, piAll.Length, piCommon.Length);

      Type nullableTypeDef = typeof(Nullable<int>).GetGenericTypeDefinition();

      ParameterInfo[] pi = new ParameterInfo[piCommon.Length];
      int[] piInd = new int[piCommon.Length];
      bool[] piNullable = new bool[piCommon.Length];

      for (int i = 0; i < piCommon.Length; i++)
      {
        pi[i] = piCommon[i].ParameterInfo;
        piInd[i] = piCommon[i].ReaderField.Ordinal;
        piNullable[i] = !piCommon[i].ParameterInfo.ParameterType.IsValueType
          || (piCommon[i].ParameterInfo.ParameterType.IsGenericType && piCommon[i].ParameterInfo.ParameterType.GetGenericTypeDefinition().Equals(nullableTypeDef));
      }

      while (reader.Read())
      {
        object[] args = new object[piAll.Length];

        for (int i = 0; i < pi.Length; i++)
        {
          args[pi[i].Position] = reader.IsDBNull(piInd[i]) && piNullable[i] ? null : reader.GetValue(piInd[i]);
        }

        yield return (T)constructor.Invoke(args);
      }
    }

    public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto) where T : new()
    {
      return ReadAllAnonymous<T>(reader, proto, new ReadOptions());
    }

    public static IEnumerable ReadAllFirstColumn(this IDataReader reader)
    {
      while (reader.Read())
      {
        yield return reader.IsDBNull(0) ? null : reader.GetValue(0);
      }
    }

    public static IEnumerable<T> ReadAllFirstColumn<T>(this IDataReader reader)
    {
      while (reader.Read())
      {
        if (reader.IsDBNull(0))
        {
          yield return (T)((object)null);
        }
        else
        {
          yield return (T)reader.GetValue(0);
        }
      }
    }

    #endregion

    #region WriteToServer

    public static void WriteToServer(this IDataReader reader, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
    {
      BulkCopy bulk = new BulkCopy(connection, null, bulkOptions, destinationTable, reader, fields);
      bulk.WriteToServer();
    }

    public static void WriteToServer(this IDataReader reader, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      BulkCopy bulk = new BulkCopy(transaction.Connection, transaction, bulkOptions, destinationTable, reader, fields);
      bulk.WriteToServer();
    }

    public static void WriteToServer(this IDataReader reader, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
    {
      using (SqlConnection connection = new SqlConnection(connectionString))
      {
        connection.Open();
        WriteToServer(reader, bulkOptions, connection, destinationTable, fields);
      }
    }

    public static void WriteToServer(this IDataReader reader, SqlConnection connection, string destinationTable, params string[] fields)
    {
      WriteToServer(reader, new BulkOptions(), connection, destinationTable, fields);
    }

    public static void WriteToServer(this IDataReader reader, SqlTransaction transaction, string destinationTable, params string[] fields)
    {
      WriteToServer(reader, new BulkOptions(), transaction, destinationTable, fields);
    }

    public static void WriteToServer(this IDataReader reader, string connectionString, string destinationTable, params string[] fields)
    {
      WriteToServer(reader, new BulkOptions(), connectionString, destinationTable, fields);
    }

    #endregion
  }

  public class ReadOptions
  {
    public FieldsSelector FieldsSelector { get; set; }
    public FromTypeOption FromTypeOption { get; set; }
    public bool CaseSensitive { get; set; }

    public ReadOptions(FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Both)
    {
      this.FieldsSelector = fieldsSelector;
      this.FromTypeOption = fromTypeOption;
      this.CaseSensitive = caseSensitive;
    }
  }
}
