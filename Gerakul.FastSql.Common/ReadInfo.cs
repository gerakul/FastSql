using System;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Gerakul.FastSql.Common
{
    internal class ReaderField
    {
        public int Ordinal;
        public string Name;
    }

    internal abstract class ReadInfo<T> : IDisposable
    {
        public IDataReader Reader;

        public void Dispose()
        {
            Reader?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected object GetValueFromReader(int indexInReader, Type convertingType, bool isNullable, bool isEnum)
        {
            if (Reader.IsDBNull(indexInReader) && isNullable)
            {
                return null;
            }

            var value = Reader.GetValue(indexInReader);

            if (value == null)
            {
                return null;
            }

            if (isEnum)
            {
                if (value is string enumAsString)
                {
                    return Enum.Parse(convertingType, enumAsString);
                }
                else
                {
                    return Enum.ToObject(convertingType, value);
                }
            }

            return Convert.ChangeType(value, convertingType);
        }

        public abstract T GetValue();
    }

    internal static class ReadInfoFactory
    {
        private static ReadOptions GetReadOptions(ReadOptions readOptions)
        {
            if (readOptions == null)
            {
                return new ReadOptions(true);
            }
            else
            {
                return readOptions.SetDefaults();
            }
        }

        public static ReadInfoByType<T> CreateByType<T>(IDataReader reader, ReadOptions readOptions) where T : new()
        {
            ReadOptions readOpt = GetReadOptions(readOptions);

            ReaderField[] readerFields = new ReaderField[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
            }

            Type type = typeof(T);

            FieldInfo[] fiAll = new FieldInfo[0];
            if ((readOpt.FromTypeOption.Value & FromTypeOption.PublicField) == FromTypeOption.PublicField)
            {
                fiAll = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

                if ((readOpt.FromTypeOption.Value & FromTypeOption.Collection) == 0)
                {
                    fiAll = fiAll.Where(x => !Helpers.IsCollection(x.FieldType)).ToArray();
                }
            }

            PropertyInfo[] piAll = new PropertyInfo[0];
            if ((readOpt.FromTypeOption.Value & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
            {
                piAll = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

                if ((readOpt.FromTypeOption.Value & FromTypeOption.Collection) == 0)
                {
                    piAll = piAll.Where(x => !Helpers.IsCollection(x.PropertyType)).ToArray();
                }
            }

            if (readOpt.CaseSensitiveFieldsMatching.Value)
            {
                if (readerFields.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Reader field names has been duplicated");
                }

                if (piAll.Select(x => x.Name).Concat(fiAll.Select(x => x.Name)).GroupBy(x => x).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Destination object field names has been duplicated");
                }
            }
            else
            {
                if (readerFields.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Reader field names has been duplicated");
                }

                if (piAll.Select(x => x.Name.ToLowerInvariant()).Concat(fiAll.Select(x => x.Name.ToLowerInvariant()))
                    .GroupBy(x => x).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Destination object field names has been duplicated");
                }
            }

            var fiCommon = readOpt.CaseSensitiveFieldsMatching.Value ?
              fiAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray() :
              fiAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray();

            var piCommon = readOpt.CaseSensitiveFieldsMatching.Value ?
              piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray() :
              piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray();

            Helpers.CheckFieldSelection(readOpt.FieldsSelector.Value, readerFields.Length, fiAll.Length + piAll.Length, fiCommon.Length + piCommon.Length);

            FieldInfo[] fi = new FieldInfo[fiCommon.Length];
            int[] fiInd = new int[fiCommon.Length];
            bool[] fiNullable = new bool[fiCommon.Length];
            Type[] fiConvertingType = new Type[fiCommon.Length];
            bool[] fiIsEnum = new bool[fiCommon.Length];

            for (int i = 0; i < fiCommon.Length; i++)
            {
                fi[i] = fiCommon[i].FieldInfo;
                fiInd[i] = fiCommon[i].ReaderField.Ordinal;
                var underlyingType = Nullable.GetUnderlyingType(fiCommon[i].FieldInfo.FieldType);
                fiNullable[i] = !fiCommon[i].FieldInfo.FieldType.GetTypeInfo().IsValueType || underlyingType != null;
                fiConvertingType[i] = underlyingType ?? fiCommon[i].FieldInfo.FieldType;
                fiIsEnum[i] = fiConvertingType[i].GetTypeInfo().IsEnum;
            }

            PropertyInfo[] pi = new PropertyInfo[piCommon.Length];
            int[] piInd = new int[piCommon.Length];
            bool[] piNullable = new bool[piCommon.Length];
            Type[] piConvertingType = new Type[piCommon.Length];
            bool[] piIsEnum = new bool[piCommon.Length];

            for (int i = 0; i < piCommon.Length; i++)
            {
                pi[i] = piCommon[i].PropertyInfo;
                piInd[i] = piCommon[i].ReaderField.Ordinal;
                var underlyingType = Nullable.GetUnderlyingType(piCommon[i].PropertyInfo.PropertyType);
                piNullable[i] = !piCommon[i].PropertyInfo.PropertyType.GetTypeInfo().IsValueType || underlyingType != null;
                piConvertingType[i] = underlyingType ?? piCommon[i].PropertyInfo.PropertyType;
                piIsEnum[i] = piConvertingType[i].GetTypeInfo().IsEnum;
            }

            return new ReadInfoByType<T>()
            {
                Reader = reader,
                fi = fi,
                fiInd = fiInd,
                fiNullable = fiNullable,
                fiConvertingType = fiConvertingType,
                fiIsEnum = fiIsEnum,
                pi = pi,
                piInd = piInd,
                piNullable = piNullable,
                piConvertingType = piConvertingType,
                piIsEnum = piIsEnum
            };
        }

        public static ReadInfoObjects CreateObjects(IDataReader reader)
        {
            return new ReadInfoObjects()
            {
                Reader = reader,
                Len = reader.FieldCount
            };
        }

        public static ReadInfoAnonymous<T> CreateAnonymous<T>(IDataReader reader, T proto, ReadOptions readOptions)
        {
            ReadOptions readOpt = GetReadOptions(readOptions);

            ReaderField[] readerFields = new ReaderField[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
            }

            ConstructorInfo constructor = typeof(T).GetConstructors().First();
            ParameterInfo[] piAll = constructor.GetParameters();

            if (readOpt.CaseSensitiveFieldsMatching.Value)
            {
                if (readerFields.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Reader field names has been duplicated");
                }

                if (piAll.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Destination object field names has been duplicated");
                }
            }
            else
            {
                if (readerFields.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Reader field names has been duplicated");
                }

                if (piAll.GroupBy(x => x.Name.ToLowerInvariant()).Any(x => x.Count() > 1))
                {
                    throw new ArgumentException("Destination object field names has been duplicated");
                }
            }

            var piCommon = readOpt.CaseSensitiveFieldsMatching.Value ?
              piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray() :
              piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray();

            Helpers.CheckFieldSelection(readOpt.FieldsSelector.Value, readerFields.Length, piAll.Length, piCommon.Length);

            ParameterInfo[] pi = new ParameterInfo[piCommon.Length];
            int[] piInd = new int[piCommon.Length];
            bool[] piNullable = new bool[piCommon.Length];
            Type[] piConvertingType = new Type[piCommon.Length];
            bool[] piIsEnum = new bool[piCommon.Length];

            for (int i = 0; i < piCommon.Length; i++)
            {
                pi[i] = piCommon[i].ParameterInfo;
                piInd[i] = piCommon[i].ReaderField.Ordinal;
                var underlyingType = Nullable.GetUnderlyingType(piCommon[i].ParameterInfo.ParameterType);
                piNullable[i] = !piCommon[i].ParameterInfo.ParameterType.GetTypeInfo().IsValueType || underlyingType != null;
                piConvertingType[i] = underlyingType ?? piCommon[i].ParameterInfo.ParameterType;
                piIsEnum[i] = piConvertingType[i].GetTypeInfo().IsEnum;
            }

            return new ReadInfoAnonymous<T>()
            {
                Reader = reader,
                constructor = constructor,
                piAll = piAll,
                pi = pi,
                piInd = piInd,
                piNullable = piNullable,
                piConvertingType = piConvertingType,
                piIsEnum = piIsEnum
            };
        }

        public static ReadInfoFirstColumn CreateFirstColumn(IDataReader reader)
        {
            return new ReadInfoFirstColumn()
            {
                Reader = reader
            };
        }

        public static ReadInfoFirstColumn<T> CreateFirstColumn<T>(IDataReader reader)
        {
            var type = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(type);
            bool isNullable = !type.GetTypeInfo().IsValueType || underlyingType != null;
            Type convertingType = underlyingType ?? type;
            bool isEnum = convertingType.GetTypeInfo().IsEnum;

            return new ReadInfoFirstColumn<T>()
            {
                Reader = reader,
                isNullable = isNullable,
                convertingType = convertingType,
                isEnum = isEnum
            };
        }
    }

    internal class ReadInfoByType<T> : ReadInfo<T> where T : new()
    {
        public FieldInfo[] fi;
        public int[] fiInd;
        public bool[] fiNullable;
        public Type[] fiConvertingType;
        public bool[] fiIsEnum;

        public PropertyInfo[] pi;
        public int[] piInd;
        public bool[] piNullable;
        public Type[] piConvertingType;
        public bool[] piIsEnum;

        public override T GetValue()
        {
            T obj = new T();

            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(obj, GetValueFromReader(fiInd[i], fiConvertingType[i], fiNullable[i], fiIsEnum[i]));
            }

            for (int i = 0; i < pi.Length; i++)
            {
                pi[i].SetValue(obj, GetValueFromReader(piInd[i], piConvertingType[i], piNullable[i], piIsEnum[i]));
            }

            return obj;
        }
    }

    internal class ReadInfoObjects : ReadInfo<object[]>
    {
        public int Len;

        public override object[] GetValue()
        {
            object[] objects = new object[Len];
            Reader.GetValues(objects);
            return objects;
        }
    }

    internal class ReadInfoAnonymous<T> : ReadInfo<T>
    {
        public ConstructorInfo constructor;
        public ParameterInfo[] piAll;
        public ParameterInfo[] pi;
        public int[] piInd;
        public bool[] piNullable;
        public Type[] piConvertingType;
        public bool[] piIsEnum;

        public override T GetValue()
        {
            object[] args = new object[piAll.Length];

            for (int i = 0; i < pi.Length; i++)
            {
                args[pi[i].Position] = GetValueFromReader(piInd[i], piConvertingType[i], piNullable[i], piIsEnum[i]);
            }

            return (T)constructor.Invoke(args);
        }
    }

    internal class ReadInfoFirstColumn : ReadInfo<object>
    {
        public override object GetValue()
        {
            return Reader.IsDBNull(0) ? null : Reader.GetValue(0);
        }
    }

    internal class ReadInfoFirstColumn<T> : ReadInfo<T>
    {
        public bool isNullable;
        public Type convertingType;
        public bool isEnum;

        public override T GetValue()
        {
            return (T)GetValueFromReader(0, convertingType, isNullable, isEnum);
        }
    }
}
