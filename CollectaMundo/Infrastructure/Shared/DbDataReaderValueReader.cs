using System.Data.Common;

namespace CollectaMundo.Infrastructure.Shared
{
    public static class DbDataReaderValueReader
    {
        public static T? GetFieldValue<T>(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return default;
            }

            if (typeof(T) == typeof(int) && value is long longValue)
            {
                return (T)(object)(int)longValue;
            }

            if (typeof(T) == typeof(int?) && value is long nullableLongValue)
            {
                return (T)(object)(int?)nullableLongValue;
            }

            if (typeof(T) == typeof(double?) && value is double doubleValue)
            {
                return (T)(object)(double?)doubleValue;
            }

            return (T)value;
        }
        public static bool GetBooleanValue(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is long longValue)
            {
                return longValue != 0;
            }

            return Convert.ToBoolean(value);
        }

    }
}
