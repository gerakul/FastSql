using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public static class DataReaderExtensions
    {
        #region Read

        private static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(Func<ReadInfo<T>> readInfoGetter)
        {
            return Helpers.CreateAsyncEnumerable<T>(null,
              (state, ct) =>
              {
                  state.ReadInfo = readInfoGetter();
                  return Task.CompletedTask;
              });
        }

        private static IEnumerable<T> CreateEnumerable<T>(Func<ReadInfo<T>> readInfoGetter)
        {
            var info = readInfoGetter();
            IDataReader reader = info.Reader;

            while (reader.Read())
            {
                yield return info.GetValue();
            }
        }

        internal static Task<bool> ReadAsyncInternal(this IDataReader reader, CancellationToken cancellationToken)
        {
            if (reader is DbDataReader)
            {
                return ((DbDataReader)reader).ReadAsync(cancellationToken);
            }

            return Task.Run(() => reader.Read(), cancellationToken);
        }

        public static IEnumerable<object[]> ReadAll(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateObjects(reader));
        }

        public static IAsyncEnumerable<object[]> ReadAllAsync(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateObjects(reader));
        }

        public static IEnumerable<T> ReadAll<T>(this IDataReader reader, ReadOptions readOptions) where T : new()
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateByType<T>(reader, readOptions));
        }

        public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader, ReadOptions readOptions) where T : new()
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateByType<T>(reader, readOptions));
        }

        public static IEnumerable<T> ReadAll<T>(this IDataReader reader) where T : new()
        {
            return ReadAll<T>(reader, new ReadOptions());
        }

        public static IAsyncEnumerable<T> ReadAllAsync<T>(this IDataReader reader) where T : new()
        {
            return ReadAllAsync<T>(reader, new ReadOptions());
        }

        public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto, ReadOptions readOptions)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
        }

        public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto, ReadOptions readOptions)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateAnonymous(reader, proto, readOptions));
        }

        public static IEnumerable<T> ReadAllAnonymous<T>(this IDataReader reader, T proto)
        {
            return ReadAllAnonymous(reader, proto, new ReadOptions());
        }

        public static IAsyncEnumerable<T> ReadAllAnonymousAsync<T>(this IDataReader reader, T proto)
        {
            return ReadAllAnonymousAsync(reader, proto, new ReadOptions());
        }

        public static IEnumerable ReadAllFirstColumn(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
        }

        public static IAsyncEnumerable<object> ReadAllFirstColumnAsync(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn(reader));
        }

        public static IEnumerable<T> ReadAllFirstColumn<T>(this IDataReader reader)
        {
            return CreateEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
        }

        public static IAsyncEnumerable<T> ReadAllFirstColumnAsync<T>(this IDataReader reader)
        {
            return CreateAsyncEnumerable(() => ReadInfoFactory.CreateFirstColumn<T>(reader));
        }

        #endregion

        #region WriteToServer

        public static void WriteToServer(this DbDataReader reader, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = new BulkCopy(connection, null, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public static Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = new BulkCopy(connection, null, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, BulkOptions bulkOptions, SqlConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, bulkOptions, connection, destinationTable, fields);
        }

        public static void WriteToServer(this DbDataReader reader, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = new BulkCopy(transaction.Connection, transaction, bulkOptions, destinationTable, reader, fields);
            bulk.WriteToServer();
        }

        public static Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken,
          BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            BulkCopy bulk = new BulkCopy(transaction.Connection, transaction, bulkOptions, destinationTable, reader, fields);
            return bulk.WriteToServerAsync(cancellationToken);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, BulkOptions bulkOptions, SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, bulkOptions, transaction, destinationTable, fields);
        }

        public static void WriteToServer(this DbDataReader reader, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                WriteToServer(reader, bulkOptions, connection, destinationTable, fields);
            }
        }

        public static async Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken,
          BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                await WriteToServerAsync(reader, cancellationToken, bulkOptions, connection, destinationTable, fields).ConfigureAwait(false);
            }
        }

        public static Task WriteToServerAsync(this DbDataReader reader, BulkOptions bulkOptions, string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, bulkOptions, connectionString, destinationTable, fields);
        }

        public static void WriteToServer(this DbDataReader reader, SqlConnection connection, string destinationTable, params string[] fields)
        {
            WriteToServer(reader, new BulkOptions(), connection, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken,
          SqlConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, cancellationToken, new BulkOptions(), connection, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, SqlConnection connection, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, new BulkOptions(), connection, destinationTable, fields);
        }

        public static void WriteToServer(this DbDataReader reader, SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            WriteToServer(reader, new BulkOptions(), transaction, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken,
          SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, cancellationToken, new BulkOptions(), transaction, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, SqlTransaction transaction, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, new BulkOptions(), transaction, destinationTable, fields);
        }

        public static void WriteToServer(this DbDataReader reader, string connectionString, string destinationTable, params string[] fields)
        {
            WriteToServer(reader, new BulkOptions(), connectionString, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, CancellationToken cancellationToken,
          string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, cancellationToken, new BulkOptions(), connectionString, destinationTable, fields);
        }

        public static Task WriteToServerAsync(this DbDataReader reader, string connectionString, string destinationTable, params string[] fields)
        {
            return WriteToServerAsync(reader, CancellationToken.None, new BulkOptions(), connectionString, destinationTable, fields);
        }

        #endregion

        internal static IEnumerable<string> GetColumnNames(this IDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string name = reader.GetName(i);
                yield return name;
            }
        }

        public static IEnumerable<ColumnDefinition> GetColumnDefinitions(this IDataReader reader, ColumnDefinitionOptions options = null, bool ignoreSchemaTable = false)
        {
            if (reader is SqlDataReader && !ignoreSchemaTable)
            {
                var sqlReader = (SqlDataReader)reader;
                var schema = sqlReader.GetColumnSchema();
                var optionKeys = options?.PrimaryKey?.Select(x => x.ToLowerInvariant()).ToArray() ?? new string[0];

                foreach (var row in schema)
                {
                    string name = row.ColumnName;
                    yield return new ColumnDefinition()
                    {
                        Name = name,
                        TypeName = row.DataTypeName,
                        IsPrimaryKey = row.IsKey ?? optionKeys.Contains(name.ToLowerInvariant()),
                        MaxLength = checked((short)((row.IsLong ?? false) ? -1 : (row.ColumnSize ?? -1))),
                        Precision = checked((byte)(row.NumericPrecision ?? 0)),
                        Scale = checked((byte)(row.NumericScale ?? 0)),
                        IsNullable = row.AllowDBNull ?? false
                    };
                }
            }
            else
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Type type = reader.GetFieldType(i);
                    string name = reader.GetName(i);
                    yield return ColumnDefinition.FromFieldType(type, name, options);
                }
            }
        }
    }
}
