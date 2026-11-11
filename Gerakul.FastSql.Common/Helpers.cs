using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

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
                    throw new ArgumentException($"Unknown FieldsSelector. Value: {fieldSelector}");
            }
        }

        // the token given to a query method and the one given to the enumeration both cancel the database calls;
        // the source is disposed with the enumeration, null when one token is enough
        internal static CancellationTokenSource? LinkTokens(CancellationToken queryToken, CancellationToken enumerationToken,
            out CancellationToken cancellationToken)
        {
            if (queryToken.CanBeCanceled && enumerationToken.CanBeCanceled && queryToken != enumerationToken)
            {
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(queryToken, enumerationToken);
                cancellationToken = linkedTokenSource.Token;
                return linkedTokenSource;
            }

            cancellationToken = queryToken.CanBeCanceled ? queryToken : enumerationToken;
            return null;
        }

        internal static void GetMembers(Type type, FromTypeOption fromTypeOption, bool forReading, out FieldInfo[] fields, out PropertyInfo[] properties)
        {
            var allFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var allProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetIndexParameters().Length == 0).ToArray(); // indexers can't be used without an index

            // a member hidden with "new" is returned together with the hiding one; keep the most derived
            var visible = allFields.Cast<MemberInfo>().Concat(allProperties)
                .GroupBy(x => x.Name)
                .Select(x => x.OrderByDescending(y => GetDepth(y.DeclaringType!)).First())
                .ToArray();

            fields = [];
            if ((fromTypeOption & FromTypeOption.PublicField) == FromTypeOption.PublicField)
            {
                fields = allFields.Where(x => visible.Contains(x)).ToArray();

                if ((fromTypeOption & FromTypeOption.Collection) == 0)
                {
                    fields = fields.Where(x => !IsCollection(x.FieldType)).ToArray();
                }
            }

            properties = [];
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
                !interfaces.Contains(typeof(IEnumerable<byte>)) && type != typeof(string);
        }
    }
}
