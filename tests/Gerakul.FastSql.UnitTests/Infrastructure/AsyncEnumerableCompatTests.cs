using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    // checks the test infrastructure itself
    public class AsyncEnumerableCompatTests
    {
        private class TrackingEnumerable : IEnumerable<int>
        {
            private readonly int count;

            public int Pulled;
            public bool Disposed;

            public TrackingEnumerable(int count)
            {
                this.count = count;
            }

            public IEnumerator<int> GetEnumerator()
            {
                try
                {
                    for (int i = 1; i <= count; i++)
                    {
                        Pulled++;
                        yield return i;
                    }
                }
                finally
                {
                    Disposed = true;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Fact]
        public async Task ToListCompat_ReadsAllItems()
        {
            var result = await new[] { 1, 2, 3 }.ToAsyncEnumerableCompat().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task TakeCompat_ReadsRequestedItemsAndDisposesSource()
        {
            var source = new TrackingEnumerable(5);

            var result = await source.ToAsyncEnumerableCompat().TakeCompat(2, TestContext.Current.CancellationToken);

            result.Should().Equal(1, 2);
            source.Pulled.Should().Be(2);
            source.Disposed.Should().BeTrue();
        }

        [Fact]
        public async Task GetEnumeratorCompat_EnumeratesItems()
        {
            using (var enumerator = new[] { 10, 20 }.ToAsyncEnumerableCompat().GetEnumeratorCompat(TestContext.Current.CancellationToken))
            {
                (await enumerator.MoveNextAsync()).Should().BeTrue();
                enumerator.Current.Should().Be(10);
                (await enumerator.MoveNextAsync()).Should().BeTrue();
                enumerator.Current.Should().Be(20);
                (await enumerator.MoveNextAsync()).Should().BeFalse();
            }
        }

        [Fact]
        public async Task ToListCompat_ReadsLibraryAsyncSequence()
        {
            var employees = new[]
            {
                new Employee() { ID = 1, CompanyID = 1, Name = "John Smith" },
                new Employee() { ID = 2, CompanyID = 2, Name = "Kate Donahue" }
            };

            var result = await employees.ToDataReader().ReadAllAsync<Employee>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(employees);
        }

        [Fact]
        public async Task ToAsyncEnumerableCompat_IsAcceptedByLibrary()
        {
            var source = new[] { new Employee() { ID = 1, Name = "John Smith" } }.ToAsyncEnumerableCompat();

            var result = await source.ToDataReader().ReadAllAsync<Employee>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.Name.Should().Be("John Smith");
        }
    }
}
