using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I5: laziness of the query methods, sessions opened by connection string contexts, cancellation tokens (Q04, Q06)
    // and command timeouts against the server. Concrete classes are in the provider folders.
    public abstract class ExecutionSemanticsTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        private const string DestinationSelectorMessage =
            "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";

        protected ExecutionSemanticsTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        protected static CancellationToken Token => TestContext.Current.CancellationToken;

        // a statement that keeps the server busy for the given number of seconds
        protected abstract string SleepCommandText(int seconds);

        // the exception a driver operation produces for an already cancelled token; operation is one of
        // OpenAsync, ExecuteReaderAsync, ExecuteScalarAsync, ExecuteNonQueryAsync
        protected abstract Type CancellationExceptionType(string operation);

        // checks the exception of a command that ran longer than its CommandTimeout
        protected abstract void AssertCommandTimeout(Exception exception);

        #region Laziness

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void QueryMethods_NotEnumerated_DoNotExecuteCommand(ContextKind kind)
        {
            var table = CreateTable();
            var sql = InsertAndSelect(table);

            Database.Run(kind, x =>
            {
                x.CreateSimple(sql).ExecuteQuery();
                x.CreateSimple(sql).ExecuteQuery<IdRow>();
                x.CreateSimple(sql).ExecuteQueryAnonymous(new { ID = 0 });
                x.CreateSimple(sql).ExecuteQueryFirstColumn();
                x.CreateSimple(sql).ExecuteQueryFirstColumn<int>();
            });
            var countBeforeEnumeration = CountRows(table);
            var ids = Database.Run(kind, x => x.CreateSimple(sql).ExecuteQueryFirstColumn<int>().ToArray());

            countBeforeEnumeration.Should().Be(0);
            ids.Should().Equal(1);
            CountRows(table).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task QueryMethods_NotEnumerated_DoNotExecuteCommandAsync(ContextKind kind)
        {
            var table = CreateTable();
            var sql = InsertAndSelect(table);

            await Database.RunAsync(kind, x =>
            {
                x.CreateSimple(sql).ExecuteQueryAsync(Token);
                x.CreateSimple(sql).ExecuteQueryAsync<IdRow>(cancellationToken: Token);
                x.CreateSimple(sql).ExecuteQueryAnonymousAsync(new { ID = 0 }, cancellationToken: Token);
                x.CreateSimple(sql).ExecuteQueryFirstColumnAsync(Token);
                x.CreateSimple(sql).ExecuteQueryFirstColumnAsync<int>(Token);
                return Task.CompletedTask;
            });
            var countBeforeEnumeration = CountRows(table);
            var ids = await Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token));

            countBeforeEnumeration.Should().Be(0);
            ids.Should().Equal(1);
            CountRows(table).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void QueryMethods_InvalidSql_FailOnlyOnEnumeration(ContextKind kind)
        {
            var sql = "select * from no_such_table";
            var failures = new List<Exception>();

            Database.Run(kind, x =>
            {
                // creating the queries does not throw
                var queries = new System.Collections.IEnumerable[]
                {
                    x.CreateSimple(sql).ExecuteQuery(),
                    x.CreateSimple(sql).ExecuteQuery<IdRow>(),
                    x.CreateSimple(sql).ExecuteQueryAnonymous(new { ID = 0 }),
                    x.CreateSimple(sql).ExecuteQueryFirstColumn(),
                    x.CreateSimple(sql).ExecuteQueryFirstColumn<int>()
                };

                foreach (var query in queries)
                {
                    failures.Add(Catch(() => query.Cast<object>().ToArray()));
                }
            });

            failures.Should().HaveCount(5).And.AllSatisfy(x => x.Should().BeAssignableTo<DbException>());
        }

        [Fact]
        public void QueryMethods_ConnectionStringContextNotEnumerated_OpenNoSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            var queries = new System.Collections.IEnumerable[]
            {
                context.CreateSimple(SelectEmployees()).ExecuteQuery(),
                context.CreateSimple(SelectEmployees()).ExecuteQuery<Employee>(),
                context.CreateMapped(SelectEmployeesOfCompany(), new { CompanyID = 1 }).ExecuteQueryAnonymous(new { ID = 0 }, new ReadOptions(FieldsSelector.Common)),
                context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn(),
                context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn<int>()
            };

            queries.Should().AllSatisfy(x => x.Should().NotBeNull());
            Database.CountSessions(applicationName).Should().Be(0);
        }

        [Fact]
        public void QueryMethodsAsync_ConnectionStringContextNotEnumerated_OpenNoSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            var queries = new object[]
            {
                context.CreateSimple(SelectEmployees()).ExecuteQueryAsync(Token),
                context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token),
                context.CreateMapped(SelectEmployeesOfCompany(), new { CompanyID = 1 })
                    .ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), Token),
                context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync(Token),
                context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(Token)
            };

            queries.Should().AllSatisfy(x => x.Should().NotBeNull());
            Database.CountSessions(applicationName).Should().Be(0);
        }

        #endregion

        #region Sessions of connection string contexts

        [Fact]
        public void QueryMethods_FullEnumeration_NoLeakedSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            context.CreateSimple(SelectEmployees()).ExecuteQuery().ToArray().Should().HaveCount(5);
            context.CreateSimple(SelectEmployees()).ExecuteQuery<Employee>().ToArray().Should().HaveCount(5);
            context.CreateSimple(SelectEmployees()).ExecuteQueryAnonymous(new { ID = 0 }, new ReadOptions(FieldsSelector.Common)).ToArray().Should().HaveCount(5);
            context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn().Cast<object>().ToArray().Should().HaveCount(5);
            context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn<int>().ToArray().Should().HaveCount(5);

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task QueryMethodsAsync_FullEnumeration_NoLeakedSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAsync(Token).ToListCompat(Token)).Should().HaveCount(5);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token).ToListCompat(Token)).Should().HaveCount(5);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), Token)
                .ToListCompat(Token)).Should().HaveCount(5);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync(Token).ToListCompat(Token)).Should().HaveCount(5);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token)).Should().HaveCount(5);

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void ExecuteQuery_EnumerationOpen_ExactlyOneSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var query = context.CreateSimple(SelectEmployees()).ExecuteQuery<Employee>();

            using (var enumerator = query.GetEnumerator())
            {
                Database.CountSessions(applicationName).Should().Be(0);

                enumerator.MoveNext().Should().BeTrue();
                Database.WaitForSessionCount(applicationName, 1).Should().Be(1);

                enumerator.MoveNext().Should().BeTrue();
                Database.CountSessions(applicationName).Should().Be(1);
            }

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task ExecuteQueryAsync_EnumerationOpen_ExactlyOneSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var query = context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token);

            using (var enumerator = query.GetEnumeratorCompat(Token))
            {
                Database.CountSessions(applicationName).Should().Be(0);

                (await enumerator.MoveNextAsync()).Should().BeTrue();
                Database.WaitForSessionCount(applicationName, 1).Should().Be(1);

                (await enumerator.MoveNextAsync()).Should().BeTrue();
                Database.CountSessions(applicationName).Should().Be(1);
            }

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void QueryMethods_PartialEnumerationThenDispose_NoLeakedSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            context.CreateSimple(SelectEmployees()).ExecuteQuery().Take(2).ToArray().Should().HaveCount(2);
            context.CreateSimple(SelectEmployees()).ExecuteQuery<Employee>().Take(2).ToArray().Should().HaveCount(2);
            context.CreateSimple(SelectEmployees()).ExecuteQueryAnonymous(new { ID = 0 }, new ReadOptions(FieldsSelector.Common)).Take(2).ToArray()
                .Should().HaveCount(2);
            context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn().Cast<object>().Take(2).ToArray().Should().HaveCount(2);
            context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumn<int>().Take(2).ToArray().Should().HaveCount(2);

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task QueryMethodsAsync_PartialEnumerationThenDispose_NoLeakedSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAsync(Token).TakeCompat(2, Token)).Should().HaveCount(2);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token).TakeCompat(2, Token)).Should().HaveCount(2);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), Token)
                .TakeCompat(2, Token)).Should().HaveCount(2);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync(Token).TakeCompat(2, Token)).Should().HaveCount(2);
            (await context.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(Token).TakeCompat(2, Token)).Should().HaveCount(2);

            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void QueryMethods_ExceptionDuringEnumeration_NoLeakedSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var ages = new List<int>();

            // the second employee has no age, which cannot be converted to int
            Action conversionFails = () =>
            {
                foreach (var age in context.CreateSimple(SelectAges()).ExecuteQueryFirstColumn<int>())
                {
                    ages.Add(age);
                }
            };
            Action mappingFails = () => context.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>().ToArray();
            Action serverFails = () => context.CreateSimple("select * from no_such_table").ExecuteQuery().ToArray();

            conversionFails.Should().Throw<InvalidCastException>();
            mappingFails.Should().Throw<InvalidOperationException>();
            serverFails.Should().Throw<DbException>();

            ages.Should().Equal(46);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task QueryMethodsAsync_ExceptionDuringEnumeration_NoLeakedSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var ages = new List<int>();

            Func<Task> conversionFails = async () =>
            {
                using (var enumerator = context.CreateSimple(SelectAges()).ExecuteQueryFirstColumnAsync<int>(Token).GetEnumeratorCompat(Token))
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        ages.Add(enumerator.Current);
                    }
                }
            };
            Func<Task> mappingFails = () => context.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token);
            Func<Task> serverFails = () => context.CreateSimple("select * from no_such_table").ExecuteQueryAsync(Token).ToListCompat(Token);

            await conversionFails.Should().ThrowAsync<InvalidCastException>();
            await mappingFails.Should().ThrowAsync<InvalidOperationException>();
            await serverFails.Should().ThrowAsync<DbException>();

            ages.Should().Equal(46);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void UseReader_ConnectionStringContext_OneSessionInsideNoneAfterwards()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var sessionsInside = -1;

            context.CreateSimple(SelectEmployees()).UseReader(r => { sessionsInside = Database.CountSessions(applicationName); });
            Action throwingAction = () => context.CreateSimple(SelectEmployees()).UseReader(r => throw new ActionException());

            throwingAction.Should().Throw<ActionException>();
            sessionsInside.Should().Be(1);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task UseReaderAsync_ConnectionStringContext_OneSessionInsideNoneAfterwardsAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var sessionsInside = new List<int>();

            await context.CreateSimple(SelectEmployees()).UseReaderAsync(Token, r =>
            {
                sessionsInside.Add(Database.CountSessions(applicationName));
                return Task.CompletedTask;
            });
            await context.CreateSimple(SelectEmployees()).UseReaderAsync(r =>
            {
                sessionsInside.Add(Database.CountSessions(applicationName));
                return Task.CompletedTask;
            });
            Func<Task> throwingAction = () => context.CreateSimple(SelectEmployees()).UseReaderAsync(Token, r => throw new ActionException());

            await throwingAction.Should().ThrowAsync<ActionException>();
            sessionsInside.Should().Equal(1, 1);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void ExecuteNonQueryAndScalar_ConnectionStringContext_NoLeakedSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var table = CreateTable();

            context.CreateSimple($"insert into {table} ({Q("ID")}) values (1)").ExecuteNonQuery().Should().Be(1);
            context.CreateSimple($"select count(*) from {table}").ExecuteScalar().Should().NotBeNull();
            Action serverFails = () => context.CreateSimple("select * from no_such_table").ExecuteScalar();

            serverFails.Should().Throw<DbException>();
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task ExecuteNonQueryAndScalarAsync_ConnectionStringContext_NoLeakedSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var table = CreateTable();

            (await context.CreateSimple($"insert into {table} ({Q("ID")}) values (1)").ExecuteNonQueryAsync(Token)).Should().Be(1);
            (await context.CreateSimple($"select count(*) from {table}").ExecuteScalarAsync(Token)).Should().NotBeNull();
            Func<Task> serverFails = () => context.CreateSimple("select * from no_such_table").ExecuteNonQueryAsync(Token);

            await serverFails.Should().ThrowAsync<DbException>();
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void WriteToServer_ConnectionStringContext_NoLeakedSession()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var table = CreateBulkTable();
            var sessionsWhileReading = -1;

            BulkRows().WriteToServer(context, table);
            context.CreateSimple(SelectCompanyIdAndName()).WriteToServer(context, table);
            context.CreateSimple(SelectCompanyIdAndName()).UseReader(r =>
            {
                sessionsWhileReading = Database.CountSessions(applicationName);
                r.WriteToServer(context, table);
            });

            sessionsWhileReading.Should().Be(1);
            CountRows(table).Should().Be(6);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public async Task WriteToServerAsync_ConnectionStringContext_NoLeakedSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var table = CreateBulkTable();

            await BulkRows().WriteToServerAsync(context, table, Token);
            await context.CreateSimple(SelectCompanyIdAndName()).WriteToServerAsync(context, table, Token);

            CountRows(table).Should().Be(4);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Fact]
        public void WriteToServer_EnumerableSource_OneSessionWhileCopying()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var table = CreateBulkTable();
            var sessionsWhileCopying = -1;

            // the rows are produced while the copy runs, so the sessions can be counted in the middle of it
            var rows = Enumerable.Range(1, 3).Select(x =>
            {
                if (x == 2)
                {
                    sessionsWhileCopying = Database.CountSessions(applicationName);
                }

                return new BulkRow() { ID = x, Name = "row " + x };
            });

            rows.WriteToServer(context, table);

            sessionsWhileCopying.Should().Be(1);
            CountRows(table).Should().Be(3);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        #endregion

        #region Data readers on scoped contexts

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        public void ExecuteQuery_ScopedContextMappingFails_ReaderDisposed(ContextKind kind)
        {
            using (var connection = Database.OpenConnection())
            {
                var transaction = kind == ContextKind.Transaction ? connection.BeginTransaction() : null;
                var context = CreateScopedContext(connection, transaction);

                var mappingFails = Catch(() => context.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>().ToArray());
                var name = context.CreateSimple(SelectCompanyNames()).ExecuteScalar();
                transaction?.Commit();

                mappingFails.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DestinationSelectorMessage);
                name.Should().Be("Mega Co");
            }
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Fixed", "Q70")]
        public async Task ExecuteQueryAsync_ScopedContextMappingFails_ReaderDisposedAsync(ContextKind kind)
        {
            using (var connection = await Database.OpenConnectionAsync())
            {
                var transaction = kind == ContextKind.Transaction ? connection.BeginTransaction() : null;
                var context = CreateScopedContext(connection, transaction);

                var mappingFails = await CatchAsync(() => context.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(cancellationToken: Token)
                    .ToListCompat(Token));
                var name = await context.CreateSimple(SelectCompanyNames()).ExecuteScalarAsync(Token);
                transaction?.Commit();

                mappingFails.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DestinationSelectorMessage);
                name.Should().Be("Mega Co");
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_ConnectionStringContextMappingFails_NextCommandRunsAsync()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            var mappingFails = await CatchAsync(() => context.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(cancellationToken: Token)
                .ToListCompat(Token));
            var name = await context.CreateSimple(SelectCompanyNames()).ExecuteScalarAsync(Token);

            mappingFails.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DestinationSelectorMessage);
            name.Should().Be("Mega Co");
        }

        #endregion

        #region Cancellation

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task ExecuteQueryAsync_CancelledTokenPassedToMethod_FirstMoveNextThrowsAsync(ContextKind kind)
        {
            var cancelled = new CancellationToken(true);
            var operation = kind == ContextKind.ConnectionString ? "OpenAsync" : "ExecuteReaderAsync";

            var exceptions = await Database.RunAsync(kind, async x => new[]
            {
                await CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: cancelled).ToListCompat(Token)),
                await CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteQueryAsync(cancelled).ToListCompat(Token)),
                await CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), cancelled)
                    .ToListCompat(Token)),
                await CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync(cancelled).ToListCompat(Token)),
                await CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(cancelled).ToListCompat(Token))
            });

            exceptions.Should().AllSatisfy(x => x.Should().BeOfType(CancellationExceptionType(operation)));
        }

        // the token given to the method stops the enumeration while it reads rows, not only the start
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "Q122")]
        public async Task ExecuteQueryAsync_TokenPassedToMethodCancelledWhileReading_NextMoveNextThrowsAsync(ContextKind kind)
        {
            var read = 0;

            var exception = await Database.RunAsync(kind, async x =>
            {
                using (var cancellation = new CancellationTokenSource())
                using (var enumerator = x.CreateSimple(SelectCompanyNames()).ExecuteQueryFirstColumnAsync<string>(cancellation.Token).GetEnumeratorCompat(Token))
                {
                    (await enumerator.MoveNextAsync()).Should().BeTrue();
                    read++;
                    cancellation.Cancel();

                    return await CatchAsync(() => enumerator.MoveNextAsync());
                }
            });

            exception.Should().BeAssignableTo<OperationCanceledException>();
            read.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteQueryAsync_ConnectionStringContextCancelledToken_OpensNoSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);
            var query = context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: new CancellationToken(true));

            var exception = await CatchAsync(() => query.ToListCompat(Token));

            exception.Should().BeAssignableTo<OperationCanceledException>();
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task ExecuteScalarAsync_CancelledToken_ThrowsAsync(ContextKind kind)
        {
            var exception = await Database.RunAsync(kind, x => CatchAsync(() => x.CreateSimple(SelectEmployees()).ExecuteScalarAsync(new CancellationToken(true))));

            exception.Should().BeOfType(CancellationExceptionType("ExecuteScalarAsync"));
        }

        [Fact]
        [Trait("Fixed", "Q06")]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task ExecuteNonQueryAsync_ConnectionStringContextCancelledToken_ThrowsAsync()
        {
            var applicationName = NewApplicationName();
            var table = CreateTable();
            var context = CreateTrackedContext(applicationName);

            var exception = await CatchAsync(() => context.CreateSimple($"insert into {table} ({Q("ID")}) values (1)")
                .ExecuteNonQueryAsync(new CancellationToken(true)));

            exception.Should().BeOfType(CancellationExceptionType("OpenAsync"));
            CountRows(table).Should().Be(0);
            Database.WaitForSessionCount(applicationName, 0).Should().Be(0);
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task ExecuteNonQueryAsync_ScopedContextCancelledToken_ThrowsAsync(ContextKind kind)
        {
            var table = CreateTable();

            var exception = await Database.RunAsync(kind, x => CatchAsync(() => x.CreateSimple($"insert into {table} ({Q("ID")}) values (1)")
                .ExecuteNonQueryAsync(new CancellationToken(true))));

            exception.Should().BeOfType(CancellationExceptionType("ExecuteNonQueryAsync"));
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Fixed", "Q06")]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task UseReaderAsync_ScopedContextCancelledToken_ThrowsAsync(ContextKind kind)
        {
            var actionCalled = false;

            var exception = await Database.RunAsync(kind, x => CatchAsync(() => x.CreateSimple(SelectCompanyNames()).UseReaderAsync(new CancellationToken(true), r =>
            {
                actionCalled = true;
                return Task.CompletedTask;
            })));

            exception.Should().BeOfType(CancellationExceptionType("ExecuteReaderAsync"));
            actionCalled.Should().BeFalse();
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver exception type for a cancelled token")]
        public async Task UseReaderAsync_ConnectionStringContextCancelledToken_ThrowsAsync()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);
            var actionCalled = false;

            var exception = await CatchAsync(() => context.CreateSimple(SelectCompanyNames()).UseReaderAsync(new CancellationToken(true), r =>
            {
                actionCalled = true;
                return Task.CompletedTask;
            }));

            exception.Should().BeOfType(CancellationExceptionType("ExecuteReaderAsync"));
            actionCalled.Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q04")]
        public async Task MoveNext_CancelledToken_ReturnsFalseWithoutThrowingAsync(ContextKind kind)
        {
            var cancelled = new CancellationToken(true);

            var results = await Database.RunAsync(kind, async x =>
            {
                var list = new List<object>();
                list.Add(await x.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token).ToListCompat(cancelled));
                list.Add(await x.CreateSimple(SelectEmployees()).ExecuteQueryAsync(Token).ToListCompat(cancelled));
                list.Add(await x.CreateSimple(SelectEmployees()).ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), Token)
                    .ToListCompat(cancelled));
                list.Add(await x.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync(Token).ToListCompat(cancelled));
                list.Add(await x.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(cancelled));
                return list;
            });

            results.Should().AllSatisfy(x => ((System.Collections.ICollection)x).Count.Should().Be(0));
        }

        // The built-in IAsyncEnumerator<T> gets its token once, when it is created, so a token cancelled in the middle
        // stays cancelled: the legacy enumerator could go on with a live token on the next call, this one cannot.
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q04")]
        public async Task MoveNext_TokenCancelledInTheMiddle_ReturnsFalseAsync(ContextKind kind)
        {
            var ids = await Database.RunAsync(kind, async x =>
            {
                var result = new List<object>();
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token))
                using (var enumerator = x.CreateSimple(SelectEmployees()).ExecuteQueryFirstColumnAsync<int>(Token).GetEnumeratorCompat(cancellation.Token))
                {
                    result.Add(await enumerator.MoveNextAsync());
                    result.Add(enumerator.Current);
                    cancellation.Cancel();
                    result.Add(await enumerator.MoveNextAsync());
                    result.Add(await enumerator.MoveNextAsync());
                }

                return result;
            });

            ids.Should().Equal(true, 1, false, false);
        }

        [Fact]
        [Trait("Quirk", "Q04")]
        public async Task MoveNext_ConnectionStringContextCancelledToken_OpensNoSessionAsync()
        {
            var applicationName = NewApplicationName();
            var context = CreateTrackedContext(applicationName);

            using (var enumerator = context.CreateSimple(SelectEmployees()).ExecuteQueryAsync<Employee>(cancellationToken: Token).GetEnumeratorCompat(new CancellationToken(true)))
            {
                (await enumerator.MoveNextAsync()).Should().BeFalse();
                Database.CountSessions(applicationName).Should().Be(0);
            }
        }

        #endregion

        #region Command timeout

        // the async variants are in the provider classes: Npgsql 4 did not apply the timeout to async execution (Q71)

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception for a command timeout")]
        public void ExecuteNonQuery_CommandTimeoutShorterThanServerSleep_ThrowsDbException(ContextKind kind)
        {
            var exception = Catch(() => Database.Run(kind, x => x.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteNonQuery()));

            AssertCommandTimeout(exception);
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver exception for a command timeout")]
        public void QueryMethods_CommandTimeoutShorterThanServerSleep_ThrowDbException()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            AssertCommandTimeout(Catch(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteScalar()));
            AssertCommandTimeout(Catch(() => context.CreateSimple(new QueryOptions(1), SleepCommandText(3)).ExecuteQuery().ToArray()));
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver exception for a command timeout")]
        public void ExecuteNonQuery_ProviderCommandTimeout_ReachesServer()
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 1;

            var exception = Catch(() => Database.Run(provider, ContextKind.ConnectionString, x => x.CreateSimple(SleepCommandText(3)).ExecuteNonQuery()));

            AssertCommandTimeout(exception);
        }

        [Fact]
        public void ExecuteNonQuery_NoTimeoutOption_DriverDefaultAllowsServerSleep()
        {
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            Action act = () => context.CreateSimple(SleepCommandText(3)).ExecuteNonQuery();

            act.Should().NotThrow();
        }

        #endregion

        protected static Exception Catch(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        protected static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private ScopedContext CreateScopedContext(DbConnection connection, DbTransaction transaction)
        {
            return transaction == null ? (ScopedContext)Spec.DefaultProvider.CreateContext(connection) : Spec.DefaultProvider.CreateContext(transaction);
        }

        private ConnectionStringContext CreateTrackedContext(string applicationName)
        {
            return Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString(applicationName));
        }

        private static string NewApplicationName()
        {
            return "fastsql-" + Guid.NewGuid().ToString("N");
        }

        private static BulkRow[] BulkRows()
        {
            return new[]
            {
                new BulkRow() { ID = 10, Name = "ten" },
                new BulkRow() { ID = 11, Name = "eleven" }
            };
        }

        private string CreateBulkTable()
        {
            return Database.CreateScratchTable($"{Q("ID")} int not null, {Q("Name")} varchar(50) null");
        }

        private string CreateTable()
        {
            return Database.CreateScratchTable($"{Q("ID")} int not null");
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single());
        }

        private string Q(string identifier)
        {
            return Spec.Quote(identifier);
        }

        private string InsertAndSelect(string table)
        {
            return $"insert into {table} ({Q("ID")}) values (1); select {Q("ID")} from {table}";
        }

        private string SelectEmployees()
        {
            return $"select * from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectEmployeesOfCompany()
        {
            return $"select * from {Q("Employee")} where {Q("CompanyID")} = @CompanyID order by {Q("ID")}";
        }

        private string SelectCompanyIdAndName()
        {
            return $"select {Q("ID")}, {Q("Name")} from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectCompanyNames()
        {
            return $"select {Q("Name")} from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectAges()
        {
            return $"select {Q("Age")} from {Q("Employee")} order by {Q("ID")}";
        }

        private class IdRow
        {
            public int ID { get; set; }
        }

        private class BulkRow
        {
            public int ID { get; set; }

            public string Name { get; set; }
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
