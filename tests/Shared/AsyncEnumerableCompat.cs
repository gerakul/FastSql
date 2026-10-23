extern alias Ix;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IxGeneric = Ix::System.Collections.Generic;
using IxLinq = Ix::System.Linq;

namespace Gerakul.FastSql.Tests.Shared
{
    // The only place in the test suite that knows which IAsyncEnumerable<T> the library exposes.
    // Today it is the legacy one from System.Interactive.Async 3.2 (GetEnumerator / MoveNext(CancellationToken)).
    // When the library moves to the IAsyncEnumerable<T> built into .NET, only this file should change.
    public static class AsyncEnumerableCompat
    {
        public static async Task<List<T>> ToListCompat<T>(this IxGeneric.IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var list = new List<T>();
            using (var enumerator = source.GetEnumeratorCompat())
            {
                while (await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(enumerator.Current);
                }
            }

            return list;
        }

        // reads at most count items and then disposes the enumerator, leaving the rest unread
        public static async Task<List<T>> TakeCompat<T>(this IxGeneric.IAsyncEnumerable<T> source, int count,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var list = new List<T>();
            using (var enumerator = source.GetEnumeratorCompat())
            {
                while (list.Count < count && await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(enumerator.Current);
                }
            }

            return list;
        }

        public static CompatAsyncEnumerator<T> GetEnumeratorCompat<T>(this IxGeneric.IAsyncEnumerable<T> source)
        {
            return new CompatAsyncEnumerator<T>(source.GetEnumerator());
        }

        // produces the library's async sequence type from in-memory items (input for ToDataReader and similar)
        public static IxGeneric.IAsyncEnumerable<T> ToAsyncEnumerableCompat<T>(this IEnumerable<T> source)
        {
            return IxLinq.AsyncEnumerable.ToAsyncEnumerable(source);
        }
    }

    public sealed class CompatAsyncEnumerator<T> : IDisposable
    {
        private readonly IxGeneric.IAsyncEnumerator<T> enumerator;

        internal CompatAsyncEnumerator(IxGeneric.IAsyncEnumerator<T> enumerator)
        {
            this.enumerator = enumerator;
        }

        public T Current
        {
            get
            {
                return enumerator.Current;
            }
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return enumerator.MoveNext(cancellationToken);
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }
    }
}
