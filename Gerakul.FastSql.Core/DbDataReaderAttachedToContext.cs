using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public class DbDataReaderAttachedToContext<C, R> where C: DatabaseContext where R: DbDataReader
    {
        private C context;
        private R reader;

        public DbDataReaderAttachedToContext(C context, R reader)
        {
            this.context = context;
            this.reader = reader;
        }


        #region WriteToServer

        public void WriteToServer(BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.GetBulkCopy(connection, null, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.GetBulkCopy(connection, null, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, connection, destinationTable, fields);
        }

        public void WriteToServer(BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.GetBulkCopy(transaction.Connection, transaction, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken,
          BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = context.GetBulkCopy(transaction.Connection, transaction, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, transaction, destinationTable, fields);
        }

        public void WriteToServer(BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (var connection = context.GetConnection(connectionString))
            {
                connection.Open();
                WriteToServer(bulkOptions, connection, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (var connection = context.GetConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                await WriteToServerAsync(cancellationToken, bulkOptions, connection, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, connectionString, destinationTable, fields);
        }

        public void WriteToServer(DbConnection connection, string destinationTable, params string[] fields)
        {
            WriteToServer(context.GetDafaultBulkOptions(), connection, destinationTable, fields);
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken,
          DbConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(cancellationToken, context.GetDafaultBulkOptions(), connection, destinationTable, fields);
        }

        public Task WriteToServerAsync(DbConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, context.GetDafaultBulkOptions(), connection, destinationTable, fields);
        }

        public void WriteToServer(DbTransaction transaction, string destinationTable, params string[] fields)
        {
            WriteToServer(context.GetDafaultBulkOptions(), transaction, destinationTable, fields);
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken,
          DbTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(cancellationToken, context.GetDafaultBulkOptions(), transaction, destinationTable, fields);
        }

        public Task WriteToServerAsync(DbTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, context.GetDafaultBulkOptions(), transaction, destinationTable, fields);
        }

        public void WriteToServer(string connectionString, string destinationTable, params string[] fields)
        {
            WriteToServer(context.GetDafaultBulkOptions(), connectionString, destinationTable, fields);
        }

        public Task WriteToServerAsync(CancellationToken cancellationToken,
          string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(cancellationToken, context.GetDafaultBulkOptions(), connectionString, destinationTable, fields);
        }

        public Task WriteToServerAsync(string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, context.GetDafaultBulkOptions(), connectionString, destinationTable, fields);
        }

        #endregion
    }
}
