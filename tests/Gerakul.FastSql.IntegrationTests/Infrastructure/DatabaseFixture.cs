using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    // A freshly created and seeded database for one test class (used as a class fixture).
    // Infrastructure code talks to the database through plain ADO.NET, not through the library under test.
    public abstract class DatabaseFixture : IAsyncLifetime
    {
        private static readonly TimeSpan defaultSessionWaitTimeout = TimeSpan.FromSeconds(10);

        private readonly ContainerFixture container;

        protected DatabaseFixture(ContainerFixture container)
        {
            this.container = container;
            this.DatabaseName = NewName("fastsql_");
        }

        public ProviderSpec Spec => container.Spec;

        public string DatabaseName { get; }

        public string ConnectionString { get; private set; }

        protected abstract string SeedResourceName { get; }

        protected abstract string CountSessionsCommandText { get; }

        protected abstract string WithDatabase(string connectionString, string database);

        protected abstract string WithSessionTracking(string connectionString, string applicationName);

        protected virtual IEnumerable<string> SplitScript(string script)
        {
            yield return script;
        }

        public async ValueTask InitializeAsync()
        {
            await container.CreateDatabaseAsync(DatabaseName).ConfigureAwait(false);
            ConnectionString = WithDatabase(container.AdminConnectionString, DatabaseName);
            await ExecuteScriptAsync(ReadSeedScript()).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            // the database goes away together with the container
            return default(ValueTask);
        }

        #region Running code with each kind of context

        public void Run(ContextKind kind, Action<DbContext> action)
        {
            Run(Spec.DefaultProvider, kind, action);
        }

        public void Run(ContextProvider provider, ContextKind kind, Action<DbContext> action)
        {
            switch (kind)
            {
                case ContextKind.ConnectionString:
                    action(provider.CreateContext(ConnectionString));
                    break;
                case ContextKind.Connection:
                    using (var connection = OpenConnection())
                    {
                        action(provider.CreateContext(connection));
                    }
                    break;
                case ContextKind.Transaction:
                    using (var connection = OpenConnection())
                    using (var transaction = connection.BeginTransaction())
                    {
                        action(provider.CreateContext(transaction));
                        transaction.Commit();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown context kind");
            }
        }

        public T Run<T>(ContextKind kind, Func<DbContext, T> func)
        {
            return Run(Spec.DefaultProvider, kind, func);
        }

        public T Run<T>(ContextProvider provider, ContextKind kind, Func<DbContext, T> func)
        {
            T result = default(T);
            Run(provider, kind, x => { result = func(x); });
            return result;
        }

        public Task RunAsync(ContextKind kind, Func<DbContext, Task> action)
        {
            return RunAsync(Spec.DefaultProvider, kind, action);
        }

        public async Task RunAsync(ContextProvider provider, ContextKind kind, Func<DbContext, Task> action)
        {
            switch (kind)
            {
                case ContextKind.ConnectionString:
                    await action(provider.CreateContext(ConnectionString)).ConfigureAwait(false);
                    break;
                case ContextKind.Connection:
                    using (var connection = await OpenConnectionAsync().ConfigureAwait(false))
                    {
                        await action(provider.CreateContext(connection)).ConfigureAwait(false);
                    }
                    break;
                case ContextKind.Transaction:
                    using (var connection = await OpenConnectionAsync().ConfigureAwait(false))
                    using (var transaction = connection.BeginTransaction())
                    {
                        await action(provider.CreateContext(transaction)).ConfigureAwait(false);
                        transaction.Commit();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown context kind");
            }
        }

        public Task<T> RunAsync<T>(ContextKind kind, Func<DbContext, Task<T>> func)
        {
            return RunAsync(Spec.DefaultProvider, kind, func);
        }

        public async Task<T> RunAsync<T>(ContextProvider provider, ContextKind kind, Func<DbContext, Task<T>> func)
        {
            T result = default(T);
            await RunAsync(provider, kind, async x => { result = await func(x).ConfigureAwait(false); }).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Plain ADO.NET helpers

        public DbConnection OpenConnection()
        {
            return OpenConnection(ConnectionString);
        }

        public async Task<DbConnection> OpenConnectionAsync()
        {
            var connection = Spec.CreateConnection(ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }

        public void Execute(string commandText)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                command.ExecuteNonQuery();
            }
        }

        // creates an empty table with a unique name, for tests that modify data; returns the table name
        public string CreateScratchTable(string columnsDefinition)
        {
            var tableName = NewName("scratch_");
            Execute($"create table {tableName} ({columnsDefinition})");
            return tableName;
        }

        #endregion

        #region Session counting

        // a connection string whose sessions can be counted: no pooling, so closing a connection ends its session
        public string CreateSessionTrackedConnectionString(string applicationName)
        {
            return WithSessionTracking(ConnectionString, applicationName);
        }

        public int CountSessions(string applicationName)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = CountSessionsCommandText;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@applicationName";
                parameter.Value = applicationName;
                command.Parameters.Add(parameter);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        // servers end sessions asynchronously, so wait until the expected count shows up or the timeout passes;
        // returns the last count seen
        public int WaitForSessionCount(string applicationName, int expected, TimeSpan? timeout = null)
        {
            var stopwatch = Stopwatch.StartNew();
            int count;
            while ((count = CountSessions(applicationName)) != expected && stopwatch.Elapsed < (timeout ?? defaultSessionWaitTimeout))
            {
                Thread.Sleep(100);
            }

            return count;
        }

        #endregion

        private DbConnection OpenConnection(string connectionString)
        {
            var connection = Spec.CreateConnection(connectionString);
            connection.Open();
            return connection;
        }

        private string ReadSeedScript()
        {
            using (var stream = typeof(DatabaseFixture).Assembly.GetManifestResourceStream(SeedResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Seed script {SeedResourceName} is not embedded");
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            using (var connection = await OpenConnectionAsync().ConfigureAwait(false))
            {
                foreach (var batch in SplitScript(script))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = batch;
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        // lower case, so the name needs no quoting on PostgreSQL
        private static string NewName(string prefix)
        {
            return prefix + Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }
}
