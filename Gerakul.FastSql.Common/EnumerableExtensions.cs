using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public static partial class EnumerableExtensions
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
