using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Gerakul.FastSql
{
    public class FieldSettings<T>
    {
        public string Name { get; private set; }
        public string DataTypeName { get; private set; }
        public Type FieldType { get; private set; }
        public Func<T, object> Getter { get; private set; }
        public Func<T, bool> IsNullGetter { get; private set; }
        public bool DetermineNullByValue { get; private set; }

        private FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object> getter, Func<T, bool> isNullGetter, bool determineNullByValue)
        {
            this.Name = name;
            this.DataTypeName = dataTypeName;
            this.FieldType = fieldType;
            this.Getter = getter;
            this.IsNullGetter = isNullGetter;
            this.DetermineNullByValue = determineNullByValue;
        }

        public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object> getter, Func<T, bool> isNullGetter)
          : this(name, dataTypeName, fieldType, getter, isNullGetter, false)
        {
        }

        public FieldSettings(string name, Type fieldType, Func<T, object> getter, Func<T, bool> isNullGetter)
          : this(name, fieldType.Name, fieldType, getter, isNullGetter, false)
        {
        }

        public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object> getter)
          : this(name, dataTypeName, fieldType, getter, x => getter(x) == null, true)
        {
        }

        public FieldSettings(string name, Type fieldType, Func<T, object> getter)
          : this(name, fieldType.Name, fieldType, getter, x => getter(x) == null, true)
        {
        }

        public FieldSettings(string name)
          : this(name, typeof(T).Name, typeof(T), x => x, x => x == null, true)
        {
        }
    }

    [Flags]
    public enum FromTypeOption : int
    {
        PublicField = 1,
        PublicProperty = 2,
        Both = 3
    }

    public static class FieldSettings
    {
        public static FieldSettings<T>[] FromType<T>(FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            List<FieldSettings<T>> fieldSettings = new List<FieldSettings<T>>();

            Type type = typeof(T);

            if ((fromTypeOption & FromTypeOption.PublicField) == FromTypeOption.PublicField)
            {
                FieldInfo[] fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                fieldSettings.AddRange(fi.Select(x => new FieldSettings<T>(x.Name, x.FieldType, y => x.GetValue(y))));
            }

            if ((fromTypeOption & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
            {
                PropertyInfo[] pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                fieldSettings.AddRange(pi.Select(x => new FieldSettings<T>(x.Name, x.PropertyType, y => x.GetValue(y))));
            }

            return fieldSettings.ToArray();
        }

        public static FieldSettings<T>[] FromType<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return FromType<T>(fromTypeOption);
        }

        public static string[] GetFields<T>(FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return FromType<T>(fromTypeOption).GetNames();
        }

        public static string[] GetFields<T>(T proto, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return FromType(proto, fromTypeOption).GetNames();
        }
    }

    public static class FieldSettingsExtension
    {
        public static string[] GetNames<T>(this IEnumerable<FieldSettings<T>> values)
        {
            return values.Select(x => x.Name).ToArray();
        }
    }
}
