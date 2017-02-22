﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace Gerakul.FastSql
{
    internal static class Helpers
    {
        internal static readonly Task CompletedTask = Task.FromResult<object>(null);

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
          Func<AsyncState<T>, CancellationToken, Task> initTaskGetter)
        {
            return new AEnumerable<AsyncState<T>, T>(
              state => state.Current,

              disposeAction,

              initTaskGetter,

              async (state, ct) =>
              {
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
              }
            );
        }

        internal static void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        {
            if (queryOptions == null)
            {
                return;
            }

            if (queryOptions.CommandTimeoutSeconds.HasValue)
            {
                cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
            }
        }

        internal static string GetSqlParameterName(this string name)
        {
            return name[0] == '@' ? name.Substring(1) : name;
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