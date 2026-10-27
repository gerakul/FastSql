using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I2: UsingConnection / UsingTransaction, transaction contexts created by the caller, connection contexts
    // over closed connections, and the options of child contexts. Concrete classes are in the provider folders.
    public abstract class ContextLifecycleTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected ContextLifecycleTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        // a query returning the isolation level of the current transaction in lower case words, e.g. "read committed"
        protected abstract string CurrentIsolationLevelCommandText { get; }

        // the provider's public transaction context constructor, e.g. new SqlTransactionContext(provider, transaction)
        protected abstract TransactionContext CreateTransactionContextByConstructor(DbTransaction transaction);

        // the driver's message for an operation on a closed connection; operation is one of
        // ExecuteNonQuery, ExecuteNonQueryAsync, ExecuteReader, ExecuteReaderAsync, BeginTransaction
        protected abstract string ClosedConnectionMessage(string operation);

        // UsingTransaction exists on connection string and connection contexts; a null level calls the overload without it
        public static TheoryData<ContextKind, IsolationLevel?> TransactionOwners => new TheoryData<ContextKind, IsolationLevel?>()
        {
            { ContextKind.ConnectionString, null },
            { ContextKind.ConnectionString, IsolationLevel.Serializable },
            { ContextKind.Connection, null },
            { ContextKind.Connection, IsolationLevel.Serializable }
        };

        public static TheoryData<ContextKind, IsolationLevel, string> IsolationLevels => new TheoryData<ContextKind, IsolationLevel, string>()
        {
            { ContextKind.ConnectionString, IsolationLevel.ReadUncommitted, "read uncommitted" },
            { ContextKind.ConnectionString, IsolationLevel.ReadCommitted, "read committed" },
            { ContextKind.ConnectionString, IsolationLevel.RepeatableRead, "repeatable read" },
            { ContextKind.ConnectionString, IsolationLevel.Serializable, "serializable" },
            { ContextKind.ConnectionString, IsolationLevel.Unspecified, "read committed" },
            { ContextKind.Connection, IsolationLevel.ReadUncommitted, "read uncommitted" },
            { ContextKind.Connection, IsolationLevel.ReadCommitted, "read committed" },
            { ContextKind.Connection, IsolationLevel.RepeatableRead, "repeatable read" },
            { ContextKind.Connection, IsolationLevel.Serializable, "serializable" },
            { ContextKind.Connection, IsolationLevel.Unspecified, "read committed" }
        };

        #region UsingConnection

        [Fact]
        public void UsingConnection_ActionSucceeds_ConnectionOpenInsideAndClosedAfterwards()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            DbConnection connection = null;
            var stateInside = ConnectionState.Closed;
            string databaseInside = null;
            var sessionsInside = 0;
            var valueInside = 0;

            context.UsingConnection(x =>
            {
                connection = x.Connection;
                stateInside = x.Connection.State;
                databaseInside = x.Connection.Database;
                sessionsInside = Database.CountSessions(applicationName);
                valueInside = x.CreateSimple("select 1").ExecuteQueryFirstColumn<int>().Single();
            });

            stateInside.Should().Be(ConnectionState.Open);
            sessionsInside.Should().Be(1);
            valueInside.Should().Be(1);
            databaseInside.Should().Be(Database.DatabaseName);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task UsingConnectionAsync_ActionSucceeds_ConnectionOpenInsideAndClosedAfterwardsAsync()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            DbConnection connection = null;
            var stateInside = ConnectionState.Closed;
            string databaseInside = null;
            var sessionsInside = 0;
            var valueInside = 0;

            await context.UsingConnectionAsync(async x =>
            {
                connection = x.Connection;
                stateInside = x.Connection.State;
                databaseInside = x.Connection.Database;
                sessionsInside = Database.CountSessions(applicationName);
                valueInside = (await x.CreateSimple("select 1").ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                    .ToListCompat(TestContext.Current.CancellationToken)).Single();
            });

            stateInside.Should().Be(ConnectionState.Open);
            sessionsInside.Should().Be(1);
            valueInside.Should().Be(1);
            databaseInside.Should().Be(Database.DatabaseName);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void UsingConnection_ActionThrows_ExceptionPropagatesAndConnectionClosed()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            var exception = new ActionException();
            DbConnection connection = null;

            Action act = () => context.UsingConnection(x =>
            {
                connection = x.Connection;
                throw exception;
            });

            act.Should().Throw<ActionException>().Which.Should().BeSameAs(exception);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task UsingConnectionAsync_ActionThrows_ExceptionPropagatesAndConnectionClosedAsync()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            var exception = new ActionException();
            DbConnection connection = null;

            Func<Task> act = () => context.UsingConnectionAsync(async x =>
            {
                connection = x.Connection;
                await Task.Yield();
                throw exception;
            });

            (await act.Should().ThrowAsync<ActionException>()).Which.Should().BeSameAs(exception);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        #endregion

        #region UsingTransaction

        [Theory]
        [MemberData(nameof(TransactionOwners))]
        public void UsingTransaction_ActionSucceeds_CommitsChanges(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var table = CreateTable();
            var countInside = 0;

            UsingTransaction(owner, isolationLevel, x =>
            {
                x.Transaction.Should().NotBeNull();
                x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                countInside = CountRows(x, table);
            });

            countInside.Should().Be(1);
            CountRows(table).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(TransactionOwners))]
        public async Task UsingTransactionAsync_ActionSucceeds_CommitsChangesAsync(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var table = CreateTable();
            var countInside = 0;

            await UsingTransactionAsync(owner, isolationLevel, async x =>
            {
                x.Transaction.Should().NotBeNull();
                await x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                countInside = CountRows(x, table);
            });

            countInside.Should().Be(1);
            CountRows(table).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(TransactionOwners))]
        public void UsingTransaction_ActionThrows_RollsBackAndRethrows(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var table = CreateTable();
            var exception = new ActionException();
            var countInside = 0;

            Action act = () => UsingTransaction(owner, isolationLevel, x =>
            {
                x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                countInside = CountRows(x, table);
                throw exception;
            });

            act.Should().Throw<ActionException>().Which.Should().BeSameAs(exception);
            countInside.Should().Be(1);
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(TransactionOwners))]
        public async Task UsingTransactionAsync_ActionThrows_RollsBackAndRethrowsAsync(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var table = CreateTable();
            var exception = new ActionException();
            var countInside = 0;

            Func<Task> act = () => UsingTransactionAsync(owner, isolationLevel, async x =>
            {
                await x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                countInside = CountRows(x, table);
                throw exception;
            });

            (await act.Should().ThrowAsync<ActionException>()).Which.Should().BeSameAs(exception);
            countInside.Should().Be(1);
            CountRows(table).Should().Be(0);
        }

        // the rollback fails too (here because the action closed the connection), which must not hide the action's exception
        [Theory]
        [MemberData(nameof(TransactionOwners))]
        [Trait("Fixed", "Q123")]
        public void UsingTransaction_ActionThrowsAndRollbackFails_RethrowsActionException(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var exception = new ActionException();

            Action act = () => UsingTransaction(owner, isolationLevel, x =>
            {
                x.Transaction.Connection.Close();
                throw exception;
            });

            act.Should().Throw<ActionException>().Which.Should().BeSameAs(exception);
        }

        [Theory]
        [MemberData(nameof(TransactionOwners))]
        [Trait("Fixed", "Q123")]
        public async Task UsingTransactionAsync_ActionThrowsAndRollbackFails_RethrowsActionExceptionAsync(ContextKind owner, IsolationLevel? isolationLevel)
        {
            var exception = new ActionException();

            Func<Task> act = () => UsingTransactionAsync(owner, isolationLevel, x =>
            {
                x.Transaction.Connection.Close();
                throw exception;
            });

            (await act.Should().ThrowAsync<ActionException>()).Which.Should().BeSameAs(exception);
        }

        [Theory]
        [MemberData(nameof(IsolationLevels))]
        public void UsingTransaction_WithIsolationLevel_LevelReachesServer(ContextKind owner, IsolationLevel isolationLevel, string expected)
        {
            string actual = null;

            UsingTransaction(owner, isolationLevel, x =>
            {
                actual = x.CreateSimple(CurrentIsolationLevelCommandText).ExecuteQueryFirstColumn<string>().Single();
            });

            actual.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(IsolationLevels))]
        public async Task UsingTransactionAsync_WithIsolationLevel_LevelReachesServerAsync(ContextKind owner, IsolationLevel isolationLevel, string expected)
        {
            string actual = null;

            await UsingTransactionAsync(owner, isolationLevel, async x =>
            {
                actual = (string)await x.CreateSimple(CurrentIsolationLevelCommandText).ExecuteScalarAsync(TestContext.Current.CancellationToken);
            });

            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData(ContextKind.ConnectionString)]
        [InlineData(ContextKind.Connection)]
        public void UsingTransaction_WithoutIsolationLevel_ServerDefaultLevel(ContextKind owner)
        {
            string actual = null;

            UsingTransaction(owner, null, x =>
            {
                actual = x.CreateSimple(CurrentIsolationLevelCommandText).ExecuteQueryFirstColumn<string>().Single();
            });

            actual.Should().Be("read committed");
        }

        [Theory]
        [InlineData(ContextKind.ConnectionString)]
        [InlineData(ContextKind.Connection)]
        public async Task UsingTransactionAsync_WithoutIsolationLevel_ServerDefaultLevelAsync(ContextKind owner)
        {
            string actual = null;

            await UsingTransactionAsync(owner, null, async x =>
            {
                actual = (string)await x.CreateSimple(CurrentIsolationLevelCommandText).ExecuteScalarAsync(TestContext.Current.CancellationToken);
            });

            actual.Should().Be("read committed");
        }

        [Fact]
        public void UsingTransaction_OnConnectionStringContext_ClosesConnectionAfterwards()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            DbConnection connection = null;
            var sessionsInside = 0;

            context.UsingTransaction(x =>
            {
                connection = x.Transaction.Connection;
                sessionsInside = Database.CountSessions(applicationName);
            });

            sessionsInside.Should().Be(1);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task UsingTransactionAsync_OnConnectionStringContext_ClosesConnectionAfterwardsAsync()
        {
            var applicationName = NewApplicationName();
            var context = Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
            DbConnection connection = null;
            var sessionsInside = 0;

            await context.UsingTransactionAsync(x =>
            {
                connection = x.Transaction.Connection;
                sessionsInside = Database.CountSessions(applicationName);
                return Task.CompletedTask;
            });

            sessionsInside.Should().Be(1);
            connection.State.Should().Be(ConnectionState.Closed);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void UsingTransaction_OnConnectionContext_LeavesConnectionOpen()
        {
            var table = CreateTable();

            using (var connection = Database.OpenConnection())
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                context.UsingTransaction(x =>
                {
                    x.Transaction.Connection.Should().BeSameAs(connection);
                    x.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                });

                connection.State.Should().Be(ConnectionState.Open);
                // no transaction is pending any more, so a command without a transaction runs on the same connection
                CountRows(context, table).Should().Be(1);
            }
        }

        #endregion

        #region Nested UsingConnection and UsingTransaction

        [Fact]
        public void UsingConnection_ThenUsingTransaction_CommitsOnSameConnection()
        {
            var table = CreateTable();
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var countAfterCommit = 0;

            context.UsingConnection(c =>
            {
                c.UsingTransaction(t =>
                {
                    t.Transaction.Connection.Should().BeSameAs(c.Connection);
                    t.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                });

                countAfterCommit = CountRows(c, table);
            });

            countAfterCommit.Should().Be(1);
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public void UsingConnection_ThenUsingTransactionThrows_RollsBackAndConnectionStaysUsable()
        {
            var table = CreateTable();
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var countAfterRollback = -1;

            context.UsingConnection(c =>
            {
                Action act = () => c.UsingTransaction(t =>
                {
                    t.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                    throw new ActionException();
                });

                act.Should().Throw<ActionException>();
                c.Connection.State.Should().Be(ConnectionState.Open);
                countAfterRollback = CountRows(c, table);
                c.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (2)").ExecuteNonQuery();
            });

            countAfterRollback.Should().Be(0);
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public async Task UsingConnectionAsync_ThenUsingTransactionAsync_CommitsOnSameConnectionAsync()
        {
            var table = CreateTable();
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var countAfterCommit = 0;

            await context.UsingConnectionAsync(async c =>
            {
                await c.UsingTransactionAsync(async t =>
                {
                    t.Transaction.Connection.Should().BeSameAs(c.Connection);
                    await t.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                });

                countAfterCommit = CountRows(c, table);
            });

            countAfterCommit.Should().Be(1);
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public async Task UsingConnectionAsync_ThenUsingTransactionAsyncThrows_RollsBackAndConnectionStaysUsableAsync()
        {
            var table = CreateTable();
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var countAfterRollback = -1;

            await context.UsingConnectionAsync(async c =>
            {
                Func<Task> act = () => c.UsingTransactionAsync(async t =>
                {
                    await t.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                    throw new ActionException();
                });

                await act.Should().ThrowAsync<ActionException>();
                c.Connection.State.Should().Be(ConnectionState.Open);
                countAfterRollback = CountRows(c, table);
                await c.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (2)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });

            countAfterRollback.Should().Be(0);
            CountRows(table).Should().Be(1);
        }

        #endregion

        #region Transaction contexts created by the caller

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TransactionContext_CallerCommits_KeepsCommands(bool byConstructor)
        {
            var table = CreateTable();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                var context = CreateTransactionContext(transaction, byConstructor);

                context.Transaction.Should().BeSameAs(transaction);
                context.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                CountRows(context, table).Should().Be(1);

                transaction.Commit();
            }

            CountRows(table).Should().Be(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TransactionContext_CallerRollsBack_DiscardsCommands(bool byConstructor)
        {
            var table = CreateTable();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                var context = CreateTransactionContext(transaction, byConstructor);

                context.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQuery();
                context.CreateMapped($"insert into {table} ({Spec.Quote("ID")}) values (@ID)", new { ID = 2 }).ExecuteNonQuery();
                CountRows(context, table).Should().Be(2);

                transaction.Rollback();
            }

            CountRows(table).Should().Be(0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TransactionContext_CallerRollsBack_DiscardsCommandsAsync(bool byConstructor)
        {
            var table = CreateTable();

            using (var connection = await Database.OpenConnectionAsync())
            using (var transaction = connection.BeginTransaction())
            {
                var context = CreateTransactionContext(transaction, byConstructor);

                await context.CreateSimple($"insert into {table} ({Spec.Quote("ID")}) values (1)").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                var count = await context.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumnAsync<int>(TestContext.Current.CancellationToken)
                    .ToListCompat(TestContext.Current.CancellationToken);
                count.Should().Equal(1);

                transaction.Rollback();
            }

            CountRows(table).Should().Be(0);
        }

        #endregion

        #region Closed connections

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public void ConnectionContext_OverClosedConnection_ExecuteNonQueryThrowsInvalidOperationException()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                Action act = () => context.CreateSimple("select 1").ExecuteNonQuery();

                act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage("ExecuteNonQuery"));
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public async Task ConnectionContext_OverClosedConnection_ExecuteNonQueryAsyncThrowsInvalidOperationExceptionAsync()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                Func<Task> act = () => context.CreateSimple("select 1").ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionMessage("ExecuteNonQueryAsync"));
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public void ConnectionContext_OverClosedConnection_ExecuteQueryThrowsInvalidOperationException()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                Action act = () => context.CreateSimple("select 1").ExecuteQuery().ToList();

                act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage("ExecuteReader"));
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public async Task ConnectionContext_OverClosedConnection_ExecuteQueryAsyncThrowsInvalidOperationExceptionAsync()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);

                Func<Task> act = () => context.CreateSimple("select 1").ExecuteQueryAsync(TestContext.Current.CancellationToken)
                    .ToListCompat(TestContext.Current.CancellationToken);

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionMessage("ExecuteReaderAsync"));
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public void ConnectionContext_OverClosedConnection_UsingTransactionThrowsInvalidOperationException()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);
                var actionCalled = false;

                Action act = () => context.UsingTransaction(x => { actionCalled = true; });

                act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage("BeginTransaction"));
                actionCalled.Should().BeFalse();
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        public async Task ConnectionContext_OverClosedConnection_UsingTransactionAsyncThrowsInvalidOperationExceptionAsync()
        {
            using (var connection = Spec.CreateConnection(Database.ConnectionString))
            {
                var context = Spec.DefaultProvider.CreateContext(connection);
                var actionCalled = false;

                Func<Task> act = () => context.UsingTransactionAsync(x =>
                {
                    actionCalled = true;
                    return Task.CompletedTask;
                });

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionMessage("BeginTransaction"));
                actionCalled.Should().BeFalse();
                connection.State.Should().Be(ConnectionState.Closed);
            }
        }

        #endregion

        #region Options of child contexts

        [Fact]
        public void UsingConnection_ChildContext_InheritsOptionsOfParentContext()
        {
            var provider = Spec.CreateProvider();
            var context = CreateContextWithChangedOptions(provider);
            ConnectionContext child = null;
            Company[] companies = null;

            context.UsingConnection(x =>
            {
                child = x;
                // reading only the ID column works because the parent's Common selector is inherited
                companies = x.CreateSimple(SelectCompanyIds()).ExecuteQuery<Company>().ToArray();
            });

            AssertInheritedOptions(provider, context, child);
            companies.Select(x => x.ID).Should().Equal(1, 2);
        }

        [Fact]
        public async Task UsingConnectionAsync_ChildContext_InheritsOptionsOfParentContextAsync()
        {
            var provider = Spec.CreateProvider();
            var context = CreateContextWithChangedOptions(provider);
            ConnectionContext child = null;
            Company[] companies = null;

            await context.UsingConnectionAsync(async x =>
            {
                child = x;
                companies = (await x.CreateSimple(SelectCompanyIds()).ExecuteQueryAsync<Company>(cancellationToken: TestContext.Current.CancellationToken)
                    .ToListCompat(TestContext.Current.CancellationToken)).ToArray();
            });

            AssertInheritedOptions(provider, context, child);
            companies.Select(x => x.ID).Should().Equal(1, 2);
        }

        [Fact]
        public void UsingTransaction_OnConnectionStringContext_ChildContextInheritsOptionsOfParentContext()
        {
            var provider = Spec.CreateProvider();
            var context = CreateContextWithChangedOptions(provider);
            TransactionContext child = null;
            Company[] companies = null;

            context.UsingTransaction(x =>
            {
                child = x;
                companies = x.CreateSimple(SelectCompanyIds()).ExecuteQuery<Company>().ToArray();
            });

            AssertInheritedOptions(provider, context, child);
            companies.Select(x => x.ID).Should().Equal(1, 2);
        }

        [Fact]
        public async Task UsingTransactionAsync_OnConnectionStringContext_ChildContextInheritsOptionsOfParentContextAsync()
        {
            var provider = Spec.CreateProvider();
            var context = CreateContextWithChangedOptions(provider);
            TransactionContext child = null;

            await context.UsingTransactionAsync(x =>
            {
                child = x;
                return Task.CompletedTask;
            });

            AssertInheritedOptions(provider, context, child);
        }

        [Fact]
        public void UsingTransaction_OnConnectionContext_ChildContextInheritsOptionsOfParentContext()
        {
            var provider = Spec.CreateProvider();

            using (var connection = Database.OpenConnection())
            {
                var context = provider.CreateContext(connection);
                ChangeOptions(context);
                TransactionContext child = null;
                Company[] companies = null;

                context.UsingTransaction(IsolationLevel.ReadCommitted, x =>
                {
                    child = x;
                    companies = x.CreateSimple(SelectCompanyIds()).ExecuteQuery<Company>().ToArray();
                });

                AssertInheritedOptions(provider, context, child);
                companies.Select(x => x.ID).Should().Equal(1, 2);
            }
        }

        [Fact]
        public async Task UsingTransactionAsync_OnConnectionContext_ChildContextInheritsOptionsOfParentContextAsync()
        {
            var provider = Spec.CreateProvider();

            using (var connection = await Database.OpenConnectionAsync())
            {
                var context = provider.CreateContext(connection);
                ChangeOptions(context);
                TransactionContext child = null;

                await context.UsingTransactionAsync(IsolationLevel.ReadCommitted, x =>
                {
                    child = x;
                    return Task.CompletedTask;
                });

                AssertInheritedOptions(provider, context, child);
            }
        }

        [Fact]
        public void UsingConnection_ChildContext_SharesProviderOfParentContext()
        {
            var provider = Spec.CreateProvider();
            var context = provider.CreateContext(Database.ConnectionString);
            DbContext child = null;

            context.UsingConnection(x => { child = x; });

            child.ContextProvider.Should().BeSameAs(provider);
            child.CommandCompilator.Should().BeSameAs(provider.CommandCompilator);
        }

        #endregion

        private ConnectionStringContext CreateContextWithChangedOptions(ContextProvider provider)
        {
            var context = provider.CreateContext(Database.ConnectionString);
            ChangeOptions(context);
            return context;
        }

        private static void ChangeOptions(DbContext context)
        {
            context.QueryOptions.CommandTimeoutSeconds = 77;
            context.ReadOptions.FieldsSelector = FieldsSelector.Common;
            context.BulkOptions.CreateTable = true;
        }

        // the child context gets copies of the parent context's options; the provider's options stay unset
        private static void AssertInheritedOptions(ContextProvider provider, DbContext parent, DbContext child)
        {
            child.QueryOptions.CommandTimeoutSeconds.Should().Be(77);
            child.ReadOptions.FieldsSelector.Should().Be(FieldsSelector.Common);
            child.BulkOptions.CreateTable.Should().BeTrue();

            child.QueryOptions.Should().NotBeSameAs(parent.QueryOptions);
            child.ReadOptions.Should().NotBeSameAs(parent.ReadOptions);
            child.BulkOptions.Should().NotBeSameAs(parent.BulkOptions);
            child.QueryOptions.GetType().Should().Be(parent.QueryOptions.GetType());
            child.BulkOptions.GetType().Should().Be(parent.BulkOptions.GetType());

            provider.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
            provider.ReadOptions.FieldsSelector.Should().BeNull();
            provider.BulkOptions.CreateTable.Should().BeNull();
        }

        private string SelectCompanyIds()
        {
            return $"select {Spec.Quote("ID")} from {Spec.Quote("Company")} order by {Spec.Quote("ID")}";
        }

        private TransactionContext CreateTransactionContext(DbTransaction transaction, bool byConstructor)
        {
            return byConstructor ? CreateTransactionContextByConstructor(transaction) : Spec.DefaultProvider.CreateContext(transaction);
        }

        private void UsingTransaction(ContextKind owner, IsolationLevel? isolationLevel, Action<TransactionContext> action)
        {
            switch (owner)
            {
                case ContextKind.ConnectionString:
                    var connectionStringContext = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
                    if (isolationLevel.HasValue)
                    {
                        connectionStringContext.UsingTransaction(isolationLevel.Value, action);
                    }
                    else
                    {
                        connectionStringContext.UsingTransaction(action);
                    }
                    break;
                case ContextKind.Connection:
                    using (var connection = Database.OpenConnection())
                    {
                        var connectionContext = Spec.DefaultProvider.CreateContext(connection);
                        if (isolationLevel.HasValue)
                        {
                            connectionContext.UsingTransaction(isolationLevel.Value, action);
                        }
                        else
                        {
                            connectionContext.UsingTransaction(action);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(owner), owner, "UsingTransaction exists only on connection string and connection contexts");
            }
        }

        private async Task UsingTransactionAsync(ContextKind owner, IsolationLevel? isolationLevel, Func<TransactionContext, Task> action)
        {
            switch (owner)
            {
                case ContextKind.ConnectionString:
                    var connectionStringContext = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
                    if (isolationLevel.HasValue)
                    {
                        await connectionStringContext.UsingTransactionAsync(isolationLevel.Value, action);
                    }
                    else
                    {
                        await connectionStringContext.UsingTransactionAsync(action);
                    }
                    break;
                case ContextKind.Connection:
                    using (var connection = await Database.OpenConnectionAsync())
                    {
                        var connectionContext = Spec.DefaultProvider.CreateContext(connection);
                        if (isolationLevel.HasValue)
                        {
                            await connectionContext.UsingTransactionAsync(isolationLevel.Value, action);
                        }
                        else
                        {
                            await connectionContext.UsingTransactionAsync(action);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(owner), owner, "UsingTransaction exists only on connection string and connection contexts");
            }
        }

        private string CreateTable()
        {
            return Database.CreateScratchTable($"{Spec.Quote("ID")} int not null");
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => CountRows(x, table));
        }

        private static int CountRows(DbContext context, string table)
        {
            return context.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single();
        }

        private static string NewApplicationName()
        {
            return "fastsql-" + Guid.NewGuid().ToString("N");
        }

        private class ActionException : Exception
        {
            public ActionException()
                : base("Thrown by the test action")
            {
            }
        }
    }
}
