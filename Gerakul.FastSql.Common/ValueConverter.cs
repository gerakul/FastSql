using System.Data;
using System.Globalization;

namespace Gerakul.FastSql.Common;

// the reader field a member's value comes from, and how the value is converted for the member
internal readonly record struct ValueSource(int Ordinal, Type ConvertingType, bool IsNullable, bool IsEnum)
{
    // a reference type or Nullable<T> takes null; the value is converted to the underlying type of Nullable<T>
    public static ValueSource For(int ordinal, Type memberType)
    {
        var underlyingType = Nullable.GetUnderlyingType(memberType);
        var convertingType = underlyingType ?? memberType;
        return new ValueSource(ordinal, convertingType, !memberType.IsValueType || underlyingType != null, convertingType.IsEnum);
    }
}

// How a value that comes from the database becomes the value of a member, of a constructor parameter or of a
// scalar result. One place for the rules: enums, values that fit as they are, dates and times, everything else
// through Convert.ChangeType with the invariant culture
internal static class ValueConverter
{
    private static class Scalar<T>
    {
        public static readonly ValueSource Source = ValueSource.For(0, typeof(T));
    }

    // the value of a reader field, for the member the source describes
    public static object? FromReader(IDataReader reader, in ValueSource source)
    {
        if (reader.IsDBNull(source.Ordinal) && source.IsNullable)
        {
            return null;
        }

        var value = reader.GetValue(source.Ordinal);

        if (value == null)
        {
            return null;
        }

        // a value that fits the member as it is (an object or interface member, an array) is not converted
        if (!source.IsEnum && source.ConvertingType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert(value, source.ConvertingType, source.IsEnum);
        }
        catch (Exception exception) when (IsConversionFailure(exception))
        {
            throw Describe(exception, $"Cannot read field '{reader.GetName(source.Ordinal)}' ({Kind(value)}) into {source.ConvertingType.Name}");
        }
    }

    // The result of ExecuteScalar as T. No row (null) and a database null give null for a T that has null; for
    // another T they cannot be read, as a null field cannot be read into a member without null
    public static T FromScalar<T>(object? value)
    {
        var source = Scalar<T>.Source;

        if (value == null || value is DBNull)
        {
            if (source.IsNullable)
            {
                return default!;
            }

            throw new InvalidCastException($"Cannot read the result ({(value == null ? "no row" : "null")}) into {source.ConvertingType.Name}");
        }

        if (!source.IsEnum && source.ConvertingType.IsInstanceOfType(value))
        {
            return (T)value;
        }

        try
        {
            return (T)Convert(value, source.ConvertingType, source.IsEnum);
        }
        catch (Exception exception) when (IsConversionFailure(exception))
        {
            throw Describe(exception, $"Cannot read the result ({Kind(value)}) into {source.ConvertingType.Name}");
        }
    }

    private static object Convert(object value, Type type, bool isEnum)
    {
        if (isEnum)
        {
            return value is string enumAsString ? Enum.Parse(type, enumAsString) : Enum.ToObject(type, value);
        }

        // The drivers differ for date and time columns: Npgsql 10 returns DateOnly and TimeOnly, Microsoft.Data.SqlClient
        // DateTime and TimeSpan. Convert.ChangeType converts neither way, so both kinds of member work with both
        // drivers through these four conversions
        if (value is DateOnly dateOnly && type == typeof(DateTime))
        {
            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }

        if (value is TimeOnly timeOnly && type == typeof(TimeSpan))
        {
            return timeOnly.ToTimeSpan();
        }

        // a time part is dropped, as the type of the member says
        if (value is DateTime dateTime && type == typeof(DateOnly))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        // a negative time or one of a day or more is not a time of day: ArgumentOutOfRangeException
        if (value is TimeSpan timeSpan && type == typeof(TimeOnly))
        {
            return TimeOnly.FromTimeSpan(timeSpan);
        }

        // Convert.ChangeType accepts only IConvertible values for other types than their own. The invariant
        // culture: data must not depend on the machine's settings ("1.5" is 15 under de-DE)
        return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static bool IsConversionFailure(Exception exception)
    {
        return exception is InvalidCastException or FormatException or OverflowException or ArgumentException;
    }

    private static string Kind(object value)
    {
        return value is DBNull ? "null" : value.GetType().Name;
    }

    // the same type of exception, with what was read into what in front of the reason
    private static Exception Describe(Exception exception, string what)
    {
        string message = $"{what}: {exception.Message}";

        return exception switch
        {
            InvalidCastException => new InvalidCastException(message, exception),
            FormatException => new FormatException(message, exception),
            OverflowException => new OverflowException(message, exception),
            ArgumentOutOfRangeException => new ArgumentOutOfRangeException(message, exception),
            _ => new ArgumentException(message, exception)
        };
    }
}
