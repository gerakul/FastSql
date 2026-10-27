using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    internal static class Helpers
    {
        internal static void CheckFieldSelection(FieldsSelector fieldSelector, int sourceNum, int destinationNum, int commonNum)
        {
            switch (fieldSelector)
            {
                case FieldsSelector.Source:
                    if (sourceNum > commonNum)
                    {
                        throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination");
                    }
                    break;
                case FieldsSelector.Destination:
                    if (destinationNum > commonNum)
                    {
                        throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source");
                    }
                    break;
                case FieldsSelector.Common:
                    break;
                case FieldsSelector.Both:
                    if (sourceNum > commonNum || destinationNum > commonNum)
                    {
                        throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Both, but source fields do not match with destination fields");
                    }
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown FieldsSelector. Value: {0}", fieldSelector));
            }
        }

        internal static AEnumerable<AsyncState<T>, T> CreateAsyncEnumerable<T>(Action<AsyncState<T>> disposeAction,
          Func<AsyncState<T>, CancellationToken, Task> initTaskGetter, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new AEnumerable<AsyncState<T>, T>(
              state => state.Current,

              disposeAction,

              initTaskGetter,

              async (state, ct) =>
              {
                  // drivers may skip the token when the next row is already buffered
                  ct.ThrowIfCancellationRequested();
                  var success = await state.ReadInfo.Reader.ReadAsyncInternal(ct).ConfigureAwait(false);

                  if (success)
                  {
                      state.Current = state.ReadInfo.GetValue();
                  }
                  else
                  {
                      state.Current = default(T);
                  }

                  return success;
              },

              cancellationToken
            );
        }

        internal static void GetMembers(Type type, FromTypeOption fromTypeOption, bool forReading, out FieldInfo[] fields, out PropertyInfo[] properties)
        {
            var allFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var allProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetIndexParameters().Length == 0).ToArray(); // indexers can't be used without an index

            // a member hidden with "new" is returned together with the hiding one; keep the most derived
            var visible = allFields.Cast<MemberInfo>().Concat(allProperties)
                .GroupBy(x => x.Name)
                .Select(x => x.OrderByDescending(y => GetDepth(y.DeclaringType)).First())
                .ToArray();

            fields = new FieldInfo[0];
            if ((fromTypeOption & FromTypeOption.PublicField) == FromTypeOption.PublicField)
            {
                fields = allFields.Where(x => visible.Contains(x)).ToArray();

                if ((fromTypeOption & FromTypeOption.Collection) == 0)
                {
                    fields = fields.Where(x => !IsCollection(x.FieldType)).ToArray();
                }
            }

            properties = new PropertyInfo[0];
            if ((fromTypeOption & FromTypeOption.PublicProperty) == FromTypeOption.PublicProperty)
            {
                // reading sets the values, writing gets them
                properties = allProperties
                    .Where(x => visible.Contains(x) && (forReading ? x.GetSetMethod(true) != null : x.GetGetMethod(true) != null))
                    .ToArray();

                if ((fromTypeOption & FromTypeOption.Collection) == 0)
                {
                    properties = properties.Where(x => !IsCollection(x.PropertyType)).ToArray();
                }
            }
        }

        private static int GetDepth(Type type)
        {
            int depth = 0;
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                depth++;
            }

            return depth;
        }

        internal static bool IsCollection(Type type)
        {
            var interfaces = type.GetInterfaces();

            if (interfaces == null || interfaces.Length == 0)
            {
                return false;
            }

            return interfaces.Contains(typeof(System.Collections.IEnumerable)) &&
                !interfaces.Contains(typeof(IEnumerable<byte>)) && !type.Equals(typeof(string));
        }
    }
}
