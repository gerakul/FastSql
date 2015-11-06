using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Gerakul.FastSql
{
  internal class BulkCopy
  {
    private class Mapping
    {
      public string Source;
      public string Destination;
    }

    private SqlConnection connection;
    private SqlTransaction transaction;
    private BulkOptions bulkOptions;
    private string destinationTable;
    private IDataReader reader;
    private string[] fields;

    public BulkCopy(SqlConnection connection, SqlTransaction transaction, BulkOptions bulkOptions, string destinationTable, IDataReader reader, params string[] fields)
    {
      this.connection = connection;
      this.transaction = transaction;
      this.bulkOptions = bulkOptions;
      this.destinationTable = destinationTable;
      this.reader = reader;
      this.fields = fields;
    }

    public void WriteToServer()
    {
      string[] sourceFields = fields.Length > 0 ? fields.ToArray() : GetReaderColumns(reader).ToArray();

      Mapping[] map;

      if (bulkOptions.FieldsSelector == FieldsSelector.Source && !bulkOptions.CaseSensitive.HasValue)
      {
        map = sourceFields.Select(x => new Mapping() { Source = x, Destination = x }).ToArray();
      }
      else
      {
        string[] destFields = GetTableColumns().ToArray();

        map = bulkOptions.CaseSensitive.HasValue && bulkOptions.CaseSensitive.Value ?
          sourceFields.Join(destFields, x => x, x => x, (x, y) => new Mapping() { Source = x, Destination = y }).ToArray() :
          sourceFields.Join(destFields, x => x.ToLowerInvariant(), x => x.ToLowerInvariant(), (x, y) => new Mapping() { Source = x, Destination = y }).ToArray();

        Helpers.CheckFieldSelection(bulkOptions.FieldsSelector, sourceFields.Length, destFields.Length, map.Length);
      }

      if (map.Length > 0)
      {
        using (SqlBulkCopy bcp = new SqlBulkCopy(connection, bulkOptions.SqlBulkCopyOptions, transaction))
        {
          bcp.DestinationTableName = destinationTable;
          if (bulkOptions.BatchSize.HasValue)
          {
            bcp.BatchSize = bulkOptions.BatchSize.Value;
          }

          if (bulkOptions.BulkCopyTimeout.HasValue)
          {
            bcp.BulkCopyTimeout = bulkOptions.BulkCopyTimeout.Value;
          }

          if (bulkOptions.EnableStreaming.HasValue)
          {
            bcp.EnableStreaming = bulkOptions.EnableStreaming.Value;
          }

          foreach (var item in map)
          {
            bcp.ColumnMappings.Add(item.Source, item.Destination);
          }

          bcp.WriteToServer(reader);
        }
      }
    }

    private IEnumerable<string> GetReaderColumns(IDataReader r)
    {
      for (int i = 0; i < r.FieldCount; i++)
      {
        string name = r.GetName(i);
        yield return name;
      }
    }

    private IEnumerable<string> GetTableColumns()
    {
      SqlExecutor executor = transaction == null ? new SqlExecutor(connection) : new SqlExecutor(transaction);

      using (SqlDataReader r = executor.ExecuteReader(string.Format("select top 0 * from {0} with(nolock)", destinationTable)))
      {
        foreach (var item in GetReaderColumns(r))
        {
          yield return item;
        }
      }
    }
  }

  public class BulkOptions
  {
    public int? BatchSize { get; set; }
    public int? BulkCopyTimeout { get; set; }
    public SqlBulkCopyOptions SqlBulkCopyOptions { get; set; }
    public FieldsSelector FieldsSelector { get; set; }
    public bool? CaseSensitive { get; set; }
    public bool? EnableStreaming { get; set; }

    public BulkOptions(int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.Default,
      FieldsSelector fieldsSelector = FieldsSelector.Source, bool? caseSensitive = null, bool? enableStreaming = null)
    {
      this.BatchSize = batchSize;
      this.BulkCopyTimeout = bulkCopyTimeout;
      this.SqlBulkCopyOptions = sqlBulkCopyOptions;
      this.FieldsSelector = fieldsSelector;
      this.CaseSensitive = caseSensitive;
      this.EnableStreaming = enableStreaming;
    }
  }
}
