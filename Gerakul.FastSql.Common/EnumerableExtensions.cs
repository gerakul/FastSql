using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public static partial class EnumerableExtensions
    {
        #region ToDataReader

        private const bool defaultReaderCaseSensitive = true;

        public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, params FieldSettings<T>[] settings)
        {
            return new UniversalDataReader<T>(values, settings, defaultReaderCaseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, params FieldSettings<T>[] settings)
        {
            return new UniversalDataReader<T>(values, settings, defaultReaderCaseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, settings, caseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, settings, caseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, FromTypeOption fromTypeOption, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(fromTypeOption), caseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, FromTypeOption fromTypeOption, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(fromTypeOption), caseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IEnumerable<T> values, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(), caseSensitive);
        }

        public static UniversalDataReader<T> ToDataReader<T>(this IAsyncEnumerable<T> values, bool caseSensitive = defaultReaderCaseSensitive)
        {
            return new UniversalDataReader<T>(values, FieldSettings.FromType<T>(), caseSensitive);
        }

        #endregion

        #region WriteToServer

        public static void WriteToServer<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            values.ToDataReader().WriteToServer(context, destinationTable, bulkOptions, fields);
        }

        public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions bulkOptions, CancellationToken cancellationToken, params string[] fields)
        {
            return values.ToDataReader().WriteToServerAsync(context, destinationTable, bulkOptions, cancellationToken, fields);
        }

        public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return values.ToDataReader().WriteToServerAsync(context, destinationTable, bulkOptions, fields);
        }

        public static void WriteToServer<T>(this IEnumerable<T> values, DbContext context, string destinationTable, params string[] fields)
        {
            values.ToDataReader().WriteToServer(context, destinationTable, fields);
        }

        public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, CancellationToken cancellationToken, params string[] fields)
        {
            return values.ToDataReader().WriteToServerAsync(context, destinationTable, cancellationToken, fields);
        }

        public static Task WriteToServerAsync<T>(this IEnumerable<T> values, DbContext context, string destinationTable, params string[] fields)
        {
            return values.ToDataReader().WriteToServerAsync(context, destinationTable, fields);
        }

        #endregion

        #region Helpers

        public static FieldSettings<T>[] GetFieldSettings<T>(this IEnumerable<T> values, FromTypeOption fromTypeOption = FromTypeOption.Default)
        {
            return FieldSettings.FromType<T>(fromTypeOption);
        }

        #endregion
    }
}
