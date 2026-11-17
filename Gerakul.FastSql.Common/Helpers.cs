using System.Reflection;

namespace Gerakul.FastSql.Common;

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

    // true when a name is there more than once; a missing name (null) equals another missing name
    internal static bool TryFindDuplicate(string?[] names, StringComparer comparer, out string? duplicate)
    {
        // a few names are compared with each other, which needs no memory
        if (names.Length <= 16)
        {
            for (int i = 1; i < names.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (comparer.Equals(names[i], names[j]))
                    {
                        duplicate = names[i];
                        return true;
                    }
                }
            }
        }
        else
        {
            var seen = new HashSet<string?>(names.Length, comparer);

            foreach (var name in names)
            {
                if (!seen.Add(name))
                {
                    duplicate = name;
                    return true;
                }
            }
        }

        duplicate = null;
        return false;
    }

    // the members of a type are found once and kept: see TypeMembers
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

    // The sync UsingConnection and UsingTransaction end their scope when the delegate returns. A delegate that
    // returns a task (an async lambda does) has not finished then: what it awaits would run after the commit, or on
    // a closed connection. C# binds such a lambda to the overloads with a result, so the result is checked here,
    // before the scope ends; a transaction is rolled back. what: "the transaction would be committed",
    // "the connection would be closed"
    internal static void ThrowIfAsyncResult<TResult>(TResult result, string method, string what)
    {
        if (IsAsyncResult(typeof(TResult)) || (result != null && IsAsyncResult(result.GetType())))
        {
            // its own exception, when it fails after the scope has ended, is observed, so it does not surface later
            if (result is Task task)
            {
                task.ContinueWith(static x => _ = x.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            throw new InvalidOperationException($"{method} cannot run a delegate that returns a task: {what} before the work " +
                $"the delegate awaits is done. Use {method}Async for an async delegate.");
        }
    }

    private static bool IsAsyncResult(Type type)
    {
        return typeof(Task).IsAssignableFrom(type) || type == typeof(ValueTask)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }
}
