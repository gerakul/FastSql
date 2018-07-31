using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public class EnumerableAttachedToContext<T, C> where C : DatabaseContext
    {
        private IEnumerable<T> values;
        private C context;

        public EnumerableAttachedToContext(IEnumerable<T> values, C context)
        {
            this.values = values;
            this.context = context;
        }

        #region WriteToServer

        public void WriteToServer(BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(bulkOptions, connection, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, bulkOptions, connection, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, DbConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, connection, destinationTable, fields);
        }

        public void WriteToServer(BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(bulkOptions, transaction, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken, BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, bulkOptions, transaction, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, DbTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, transaction, destinationTable, fields);
        }

        public void WriteToServer(BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(bulkOptions, connectionString, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, bulkOptions, connectionString, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, bulkOptions, connectionString, destinationTable, fields);
        }

        public void WriteToServer(DbConnection connection, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(connection, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          DbConnection connection, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, connection, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(DbConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, connection, destinationTable, fields);
        }

        public void WriteToServer(DbTransaction transaction, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(transaction, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          DbTransaction transaction, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, transaction, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(DbTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, transaction, destinationTable, fields);
        }

        public void WriteToServer(string connectionString, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServer(connectionString, destinationTable, fields);
            }
        }

        public async Task WriteToServerAsync(CancellationToken cancellationToken,
          string connectionString, string destinationTable, params string[] fields)
        {
            using (var reader = values.ToDataReader())
            {
                await DbDataReaderAttachedToContext.Create(reader, context)
                    .WriteToServerAsync(cancellationToken, connectionString, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public Task WriteToServerAsync(string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(CancellationToken.None, connectionString, destinationTable, fields);
        }

        #endregion
    }

    public static class EnumerableAttachedToContext
    {
        public static EnumerableAttachedToContext<T, C> Create<T, C>(IEnumerable<T> values, C context) where C : DatabaseContext
        {
            return new EnumerableAttachedToContext<T, C>(values, context);
        }
    }

}
