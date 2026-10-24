using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Tests.Shared
{
    // The only place in the test suite that knows which IAsyncEnumerable<T> the library exposes.
    // Today it is the one built into .NET (GetAsyncEnumerator(CancellationToken) / MoveNextAsync()), which replaced
    // the legacy one from System.Interactive.Async 3.2 (GetEnumerator() / MoveNext(CancellationToken)).
    public static class AsyncEnumerableCompat
    {
        public static async Task<List<T>> ToListCompat<T>(this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var list = new List<T>();
            using (var enumerator = source.GetEnumeratorCompat(cancellationToken))
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    list.Add(enumerator.Current);
                }
            }

            return list;
        }

        // reads at most count items and then disposes the enumerator, leaving the rest unread
        public static async Task<List<T>> TakeCompat<T>(this IAsyncEnumerable<T> source, int count,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var list = new List<T>();
            using (var enumerator = source.GetEnumeratorCompat(cancellationToken))
            {
                while (list.Count < count && await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    list.Add(enumerator.Current);
                }
            }

            return list;
        }

        // the built-in enumerator gets its cancellation token once, when it is created
        public static CompatAsyncEnumerator<T> GetEnumeratorCompat<T>(this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new CompatAsyncEnumerator<T>(source.GetAsyncEnumerator(cancellationToken));
        }

        // produces the library's async sequence type from in-memory items (input for ToDataReader and similar)
        public static IAsyncEnumerable<T> ToAsyncEnumerableCompat<T>(this IEnumerable<T> source)
        {
            return AsyncEnumerable.ToAsyncEnumerable(source);
        }
    }

    // a synchronously disposable wrapper, so tests keep using "using" blocks
    public sealed class CompatAsyncEnumerator<T> : IDisposable
    {
        private readonly IAsyncEnumerator<T> enumerator;

        internal CompatAsyncEnumerator(IAsyncEnumerator<T> enumerator)
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

        public Task<bool> MoveNextAsync()
        {
            return enumerator.MoveNextAsync().AsTask();
        }

        public void Dispose()
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
