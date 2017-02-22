using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
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
            if (Reader != null)
            {
                Reader.Close();
            }
        }

        public abstract T GetValue();
    }

    internal class ReadInfoFactory
    {
        public static ReadInfoByType<T> CreateByType<T>(IDataReader reader, ReadOptions readOptions) where T : new()
        {
            ReadOptions readOpt = readOptions ?? new ReadOptions();

            ReaderField[] readerFields = new ReaderField[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
            }

            Type type = typeof(T);

            FieldInfo[] fiAll = new FieldInfo[0];
            if ((readOpt.FromTypeOption & FromTypeOption.PublicField) == FromTypeOption.PublicField)
            {
                fiAll = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

                if ((readOpt.FromTypeOption & FromTypeOption.Collection) == 0)
                {
                    fiAll = fiAll.Where(x => !Helpers.IsCollection(x.FieldType)).ToArray();
                }
            }

            PropertyInfo[] piAll = new PropertyInfo[0];
            if ((readOpt.FromTypeOption & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
            {
                piAll = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

                if ((readOpt.FromTypeOption & FromTypeOption.Collection) == 0)
                {
                    piAll = piAll.Where(x => !Helpers.IsCollection(x.PropertyType)).ToArray();
                }
            }

            var fiCommon = readOpt.CaseSensitive ?
              fiAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray() :
              fiAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { FieldInfo = x, ReaderField = y }).ToArray();

            var piCommon = readOpt.CaseSensitive ?
              piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray() :
              piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { PropertyInfo = x, ReaderField = y }).ToArray();

            Helpers.CheckFieldSelection(readOpt.FieldsSelector, readerFields.Length, fiAll.Length + piAll.Length, fiCommon.Length + piCommon.Length);

            Type nullableTypeDef = typeof(Nullable<int>).GetGenericTypeDefinition();

            FieldInfo[] fi = new FieldInfo[fiCommon.Length];
            int[] fiInd = new int[fiCommon.Length];
            bool[] fiNullable = new bool[fiCommon.Length];

            for (int i = 0; i < fiCommon.Length; i++)
            {
                fi[i] = fiCommon[i].FieldInfo;
                fiInd[i] = fiCommon[i].ReaderField.Ordinal;
                fiNullable[i] = !fiCommon[i].FieldInfo.FieldType.GetTypeInfo().IsValueType
                  || (fiCommon[i].FieldInfo.FieldType.GetTypeInfo().IsGenericType && fiCommon[i].FieldInfo.FieldType.GetGenericTypeDefinition().Equals(nullableTypeDef));
            }

            PropertyInfo[] pi = new PropertyInfo[piCommon.Length];
            int[] piInd = new int[piCommon.Length];
            bool[] piNullable = new bool[piCommon.Length];

            for (int i = 0; i < piCommon.Length; i++)
            {
                pi[i] = piCommon[i].PropertyInfo;
                piInd[i] = piCommon[i].ReaderField.Ordinal;
                piNullable[i] = !piCommon[i].PropertyInfo.PropertyType.GetTypeInfo().IsValueType
                  || (piCommon[i].PropertyInfo.PropertyType.GetTypeInfo().IsGenericType && piCommon[i].PropertyInfo.PropertyType.GetGenericTypeDefinition().Equals(nullableTypeDef));
            }

            return new ReadInfoByType<T>()
            {
                Reader = reader,
                fi = fi,
                fiInd = fiInd,
                fiNullable = fiNullable,
                pi = pi,
                piInd = piInd,
                piNullable = piNullable
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
            ReadOptions readOpt = readOptions ?? new ReadOptions();

            ReaderField[] readerFields = new ReaderField[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                readerFields[i] = new ReaderField() { Ordinal = i, Name = reader.GetName(i) };
            }

            ConstructorInfo constructor = typeof(T).GetConstructors().First();
            ParameterInfo[] piAll = constructor.GetParameters();

            var piCommon = readOpt.CaseSensitive ?
              piAll.Join(readerFields, x => x.Name, x => x.Name, (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray() :
              piAll.Join(readerFields, x => x.Name.ToLowerInvariant(), x => x.Name.ToLowerInvariant(), (x, y) => new { ParameterInfo = x, ReaderField = y }).ToArray();

            Helpers.CheckFieldSelection(readOpt.FieldsSelector, readerFields.Length, piAll.Length, piCommon.Length);

            Type nullableTypeDef = typeof(Nullable<int>).GetGenericTypeDefinition();

            ParameterInfo[] pi = new ParameterInfo[piCommon.Length];
            int[] piInd = new int[piCommon.Length];
            bool[] piNullable = new bool[piCommon.Length];

            for (int i = 0; i < piCommon.Length; i++)
            {
                pi[i] = piCommon[i].ParameterInfo;
                piInd[i] = piCommon[i].ReaderField.Ordinal;
                piNullable[i] = !piCommon[i].ParameterInfo.ParameterType.GetTypeInfo().IsValueType
                  || (piCommon[i].ParameterInfo.ParameterType.GetTypeInfo().IsGenericType && piCommon[i].ParameterInfo.ParameterType.GetGenericTypeDefinition().Equals(nullableTypeDef));
            }

            return new ReadInfoAnonymous<T>()
            {
                Reader = reader,
                constructor = constructor,
                piAll = piAll,
                pi = pi,
                piInd = piInd,
                piNullable = piNullable
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
            return new ReadInfoFirstColumn<T>()
            {
                Reader = reader
            };
        }
    }

    internal class ReadInfoByType<T> : ReadInfo<T> where T : new()
    {
        public FieldInfo[] fi;
        public int[] fiInd;
        public bool[] fiNullable;

        public PropertyInfo[] pi;
        public int[] piInd;
        public bool[] piNullable;

        public override T GetValue()
        {
            T obj = new T();

            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(obj, Reader.IsDBNull(fiInd[i]) && fiNullable[i] ? null : Reader.GetValue(fiInd[i]));
            }

            for (int i = 0; i < pi.Length; i++)
            {
                pi[i].SetValue(obj, Reader.IsDBNull(piInd[i]) && piNullable[i] ? null : Reader.GetValue(piInd[i]));
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

        public override T GetValue()
        {
            object[] args = new object[piAll.Length];

            for (int i = 0; i < pi.Length; i++)
            {
                args[pi[i].Position] = Reader.IsDBNull(piInd[i]) && piNullable[i] ? null : Reader.GetValue(piInd[i]);
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
        public override T GetValue()
        {
            return Reader.IsDBNull(0) ? (T)((object)null) : (T)Reader.GetValue(0);
        }
    }
}
