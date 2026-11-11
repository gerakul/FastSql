using System;
using System.Threading;
using System.Threading.Tasks;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    // One database server container per test run (registered as an assembly fixture).
    // Test classes get their own databases in it through DatabaseFixture.
    public abstract class ContainerFixture : IAsyncLifetime
    {
        private readonly SemaphoreSlim createDatabaseLock = new SemaphoreSlim(1, 1);

        protected ContainerFixture(ProviderSpec spec)
        {
            this.Spec = spec;
        }

        public ProviderSpec Spec { get; }

        public abstract string Image { get; }

        public string AdminConnectionString { get; private set; }

        public async ValueTask InitializeAsync()
        {
            try
            {
                AdminConnectionString = await StartContainerAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // fail loudly: a missing database must never look like a passing test run
                throw new InvalidOperationException($"Could not start the {Image} container. "
                    + "The integration tests need a running Docker engine with Linux containers.", ex);
            }
        }

        public ValueTask DisposeAsync()
        {
            return DisposeContainerAsync();
        }

        // creation is serialized, concurrent CREATE DATABASE statements can conflict
        public async Task CreateDatabaseAsync(string name)
        {
            await createDatabaseLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var connection = Spec.CreateConnection(AdminConnectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"create database {Spec.Quote(name)}";
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                createDatabaseLock.Release();
            }
        }

        // builds and starts the container (building is done here too, because it fails without Docker)
        // and returns the admin connection string
        protected abstract Task<string> StartContainerAsync();

        protected abstract ValueTask DisposeContainerAsync();
    }
}
