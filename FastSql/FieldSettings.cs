using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastSql
{
  public class FieldSettings<T>
  {
    public string Name { get; private set; }
    public string DataTypeName { get; private set; }
    public Type FieldType { get; private set; }
    public Func<T, object> Getter { get; private set; }
    public Func<T, bool> IsNullGetter { get; private set; }

    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object> getter, Func<T, bool> isNullGetter)
    {
      this.Name = name;
      this.DataTypeName = dataTypeName;
      this.FieldType = fieldType;
      this.Getter = getter;
      this.IsNullGetter = isNullGetter;
    }

    public FieldSettings(string name, Type fieldType, Func<T, object> getter, Func<T, bool> isNullGetter)
      : this(name, fieldType.Name, fieldType, getter, isNullGetter)
    {
    }

    public FieldSettings(string name, string dataTypeName, Type fieldType, Func<T, object> getter)
      : this(name, dataTypeName, fieldType, getter, x => getter(x) == null)
    {
    }

    public FieldSettings(string name, Type fieldType, Func<T, object> getter)
      : this(name, fieldType.Name, fieldType, getter, x => getter(x) == null)
    {
    }

    public FieldSettings(string name)
      : this(name, typeof(T).Name, typeof(T), x => x, x => x == null)
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
    public static FieldSettings<T>[] FromType<T>(FromTypeOption option = FromTypeOption.Both)
    {
      List<FieldSettings<T>> fieldSettings = new List<FieldSettings<T>>();

      Type type = typeof(T);

      if ((option & FromTypeOption.PublicField) == FromTypeOption.PublicField)
      {
        FieldInfo[] fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        fieldSettings.AddRange(fi.Select(x => new FieldSettings<T>(x.Name, x.FieldType, y => x.GetValue(y))));
      }

      if ((option & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
      {
        PropertyInfo[] pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        fieldSettings.AddRange(pi.Select(x => new FieldSettings<T>(x.Name, x.PropertyType, y => x.GetValue(y))));
      }

      return fieldSettings.ToArray();
    }
  }
}
