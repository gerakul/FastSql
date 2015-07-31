﻿using System.Data.SqlClient;

namespace FastSql
{
  public class Import
  {
    public SqlConnection ConnectionFrom { get; private set; }
    public SqlTransaction TransactionFrom { get; private set; }
    public SqlConnection ConnectionTo { get; private set; }
    public SqlTransaction TransactionTo { get; private set; }

    public Import(SqlConnection connectionFrom, SqlConnection connectionTo)
    {
      this.ConnectionFrom = connectionFrom;
      this.TransactionFrom = null;
      this.ConnectionTo = connectionTo;
      this.TransactionTo = null;
    }

    public Import(SqlConnection connectionFrom, SqlTransaction transactionTo)
    {
      this.ConnectionFrom = connectionFrom;
      this.TransactionFrom = null;
      this.ConnectionTo = transactionTo.Connection;
      this.TransactionTo = transactionTo;
    }

    public Import(SqlTransaction transactionFrom, SqlConnection connectionTo)
    {
      this.ConnectionFrom = transactionFrom.Connection;
      this.TransactionFrom = transactionFrom;
      this.ConnectionTo = connectionTo;
      this.TransactionTo = null;
    }

    public Import(SqlTransaction transactionFrom, SqlTransaction transactionTo)
    {
      this.ConnectionFrom = transactionFrom.Connection;
      this.TransactionFrom = transactionFrom;
      this.ConnectionTo = transactionTo.Connection;
      this.TransactionTo = transactionTo;
    }

    public void Execute(ImportOptions options, string commandText, string destinationTable, params object[] commandParameters)
    {
      SqlExecutor executor = TransactionFrom == null ? new SqlExecutor(ConnectionFrom) : new SqlExecutor(TransactionFrom);
      using (SqlDataReader reader = executor.ExecuteReader(options.QueryOptions, commandText, commandParameters))
      {
        if (TransactionTo == null)
        {
          reader.WriteToServer(options.BulkOptions, ConnectionTo, destinationTable);
        }
        else
        {
          reader.WriteToServer(options.BulkOptions, TransactionTo, destinationTable);
        }
      }
    }

    public void Execute(string commandText, string destinationTable, params object[] commandParameters)
    {
      Execute(new ImportOptions(), commandText, destinationTable, commandParameters);
    }

    #region Static

    public static void Execute(ImportOptions options, string connectionStringFrom, string connectionStringTo, string commandText, string destinationTable, params object[] commandParameters)
    {
      using (SqlConnection connFrom = new SqlConnection(connectionStringFrom))
      {
        connFrom.Open();

        using (SqlConnection connTo = new SqlConnection(connectionStringTo))
        {
          connTo.Open();

          Import import = new Import(connFrom, connTo);
          import.Execute(options, commandText, destinationTable, commandParameters);
        }
      }
    }

    public static void Execute(string connectionStringFrom, string connectionStringTo, string commandText, string destinationTable, params object[] commandParameters)
    {
      Execute(new ImportOptions(), connectionStringFrom, connectionStringTo, commandText, destinationTable, commandParameters);
    }

    #endregion
  }

  public class ImportOptions
  {
    public QueryOptions QueryOptions { get; private set; }
    public BulkOptions BulkOptions { get; private set; }

    public ImportOptions(QueryOptions queryOptions, BulkOptions bulkOptions = null)
    {
      this.QueryOptions = queryOptions == null ? new QueryOptions() : queryOptions;
      this.BulkOptions = bulkOptions == null ? new BulkOptions() : bulkOptions;
    }

    public ImportOptions(int? commandTimeoutSeconds = null, int? batchSize = null, int? bulkCopyTimeout = null, SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.Default, 
      FieldsSelector fieldsSelector = FieldsSelector.Source, bool? caseSensitive = null)
      : this(new QueryOptions(commandTimeoutSeconds), new BulkOptions(batchSize, bulkCopyTimeout, sqlBulkCopyOptions, fieldsSelector, caseSensitive))
    {
    }
  }
}
