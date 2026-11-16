using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // The public API that phase 4 added or changed (I10 to I18), against the databases.
    // Concrete classes are in the provider folders.
    public abstract class Phase4ApiTests<TDatabase> : DataModificationTests<TDatabase> where TDatabase : DatabaseFixture
    {
        protected Phase4ApiTests(TDatabase database)
            : base(database)
        {
        }

        protected static CancellationToken Token => TestContext.Current.CancellationToken;

        private ConnectionStringContext Context => Spec.DefaultProvider.CreateContext(Database.ConnectionString);

        private static Person[] ThreePersons => new[]
        {
            new Person() { ID = 1, GroupID = 1, Name = "Alice", Age = 30 },
            new Person() { ID = 2, GroupID = 1, Name = "Bob", Age = 40 },
            new Person() { ID = 3, GroupID = 2, Name = "Carol", Age = null }
        };

        public record PersonRecord(int ID, int GroupID, string Name, int? Age);

        private static PersonRecord[] ThreeRecords => ThreePersons.Select(x => new PersonRecord(x.ID, x.GroupID, x.Name, x.Age)).ToArray();

        #region I14: the shortcuts return the affected rows

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Shortcuts_ReturnTheNumberOfAffectedRows(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            int inserted = Database.Run(kind, x => x.Insert(table, new Person() { ID = 4, GroupID = 2, Name = "Dave" }));
            int updated = Database.Run(kind, x => x.Update(table, new Person() { ID = 2, GroupID = 1, Name = "Robert" }, "ID"));
            int updatedNone = Database.Run(kind, x => x.Update(table, new Person() { ID = 99, GroupID = 1, Name = "Nobody" }, "ID"));
            int mergedNew = Database.Run(kind, x => x.Merge(table, new Person() { ID = 5, GroupID = 3, Name = "Eve" }, "ID"));
            int mergedExisting = Database.Run(kind, x => x.Merge(table, new Person() { ID = 5, GroupID = 3, Name = "Eva" }, "ID"));
            int deletedTwo = Database.Run(kind, x => x.Delete(table, new { GroupID = 1 }));
            int deletedNone = Database.Run(kind, x => x.Delete(table, new { GroupID = 77 }));

            inserted.Should().Be(1);
            updated.Should().Be(1);
            updatedNone.Should().Be(0);
            mergedNew.Should().Be(1);
            mergedExisting.Should().Be(1);
            deletedTwo.Should().Be(2);
            deletedNone.Should().Be(0);
            ReadPersons(table).Select(x => x.Name).Should().Equal("Carol", "Dave", "Eva");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Shortcuts_ReturnTheNumberOfAffectedRowsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            int inserted = await Database.RunAsync(kind, x => x.InsertAsync(table, new Person() { ID = 4, GroupID = 2, Name = "Dave" }));
            int updated = await Database.RunAsync(kind, x => x.UpdateAsync(table, new Person() { ID = 2, GroupID = 1, Name = "Robert" }, "ID"));
            int updatedNone = await Database.RunAsync(kind, x => x.UpdateAsync(table, new Person() { ID = 99, GroupID = 1, Name = "Nobody" }, "ID"));
            int merged = await Database.RunAsync(kind, x => x.MergeAsync(table, new Person() { ID = 5, GroupID = 3, Name = "Eve" }, "ID"));
            int deletedTwo = await Database.RunAsync(kind, x => x.DeleteAsync(table, new { GroupID = 1 }));

            inserted.Should().Be(1);
            updated.Should().Be(1);
            updatedNone.Should().Be(0);
            merged.Should().Be(1);
            deletedTwo.Should().Be(2);
            ReadPersons(table).Select(x => x.Name).Should().Equal("Carol", "Dave", "Eve");
        }

        #endregion

        #region I13: ExecuteScalar<T>

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteScalar_Typed_ConvertsTheResult(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            // count(*) is int on SQL Server and bigint on PostgreSQL
            int count = Database.Run(kind, x => x.CreateSimple($"select count(*) from {table}").ExecuteScalar<int>());
            long countAsLong = Database.Run(kind, x => x.CreateSimple($"select count(*) from {table}").ExecuteScalar<long>());
            string name = Database.Run(kind, x => x.CreateSimple($"select {Q("Name")} from {table} where {Q("ID")} = @p0", 2).ExecuteScalar<string>());
            int? age = Database.Run(kind, x => x.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 2).ExecuteScalar<int?>());
            int? nullAge = Database.Run(kind, x => x.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 3).ExecuteScalar<int?>());
            int? noRow = Database.Run(kind, x => x.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 99).ExecuteScalar<int?>());
            string noName = Database.Run(kind, x => x.CreateSimple($"select {Q("Name")} from {table} where {Q("ID")} = @p0", 99).ExecuteScalar<string>());

            count.Should().Be(3);
            countAsLong.Should().Be(3L);
            name.Should().Be("Bob");
            age.Should().Be(40);
            nullAge.Should().BeNull();
            noRow.Should().BeNull();
            noName.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteScalar_Typed_ConvertsTheResultAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            int count = await Database.RunAsync(kind, x => x.CreateSimple($"select count(*) from {table}").ExecuteScalarAsync<int>(Token));
            int? nullAge = await Database.RunAsync(kind, x => x.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 3).ExecuteScalarAsync<int?>(Token));
            Func<Task> noRow = () => Database.RunAsync(kind, x => x.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 99).ExecuteScalarAsync<int>(Token));

            count.Should().Be(3);
            nullAge.Should().BeNull();
            (await noRow.Should().ThrowExactlyAsync<InvalidCastException>()).WithMessage("Cannot read the result (no row) into Int32");
        }

        [Fact]
        public void ExecuteScalar_NullIntoATypeWithoutNull_Throws()
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            Action nullValue = () => Context.CreateSimple($"select {Q("Age")} from {table} where {Q("ID")} = @p0", 3).ExecuteScalar<int>();

            nullValue.Should().ThrowExactly<InvalidCastException>().WithMessage("Cannot read the result (null) into Int32");
        }

        #endregion

        #region I12, I11: results and tokens of UsingConnection and UsingTransaction

        [Fact]
        public void UsingConnection_Func_ReturnsItsResult()
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var names = Context.UsingConnection(c => c.CreateSimple($"select {Q("Name")} from {table} order by {Q("ID")}").ExecuteQueryFirstColumn<string>().ToArray());
            int state = Context.UsingConnection(c => (int)c.Connection.State);

            names.Should().Equal("Alice", "Bob", "Carol");
            state.Should().Be((int)ConnectionState.Open);
        }

        [Fact]
        public async Task UsingConnectionAsync_Func_ReturnsItsResultAsync()
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var names = await Context.UsingConnectionAsync(c => c.CreateSimple($"select {Q("Name")} from {table} order by {Q("ID")}")
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token), Token);
            int count = await Context.UsingConnectionAsync(async c =>
            {
                await Task.Yield();
                return await c.CreateSimple($"select count(*) from {table}").ExecuteScalarAsync<int>(Token);
            }, Token);

            names.Should().Equal("Alice", "Bob", "Carol");
            count.Should().Be(3);
        }

        // the connection is closed when the method returns, so a lazy query must be read inside
        [Fact]
        public void UsingConnection_LazyQueryReturnedUnread_FailsWhenEnumerated()
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var lazy = Context.UsingConnection(c => c.CreateSimple($"select * from {table}").ExecuteQuery<Person>());

            lazy.Invoking(x => x.ToArray()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void UsingTransaction_Func_CommitsAndReturnsItsResult()
        {
            var table = CreatePersonTable();

            int fromString = Context.UsingTransaction(t =>
            {
                t.Insert(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                return t.CreateSimple($"select count(*) from {table}").ExecuteScalar<int>();
            });
            int fromConnection = Context.UsingConnection(c => c.UsingTransaction(IsolationLevel.ReadCommitted, t =>
            {
                t.Insert(table, new Person() { ID = 2, GroupID = 1, Name = "Bob" });
                return t.CreateSimple($"select count(*) from {table}").ExecuteScalar<int>();
            }));

            fromString.Should().Be(1);
            fromConnection.Should().Be(2);
            ReadPersons(table).Select(x => x.Name).Should().Equal("Alice", "Bob");
        }

        [Fact]
        public void UsingTransaction_FuncThrows_RollsBackAndThrowsTheSameException()
        {
            var table = CreatePersonTable();
            var exception = new InvalidOperationException("stop");

            Action act = () => Context.UsingTransaction<int>(t =>
            {
                t.Insert(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                throw exception;
            });

            act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(exception);
            ReadPersons(table).Should().BeEmpty();
        }

        [Fact]
        public async Task UsingTransactionAsync_Func_CommitsAndReturnsItsResultAsync()
        {
            var table = CreatePersonTable();

            int fromString = await Context.UsingTransactionAsync(async t =>
            {
                await t.InsertAsync(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                return await t.CreateSimple($"select count(*) from {table}").ExecuteScalarAsync<int>(Token);
            }, Token);
            int fromConnection = await Context.UsingConnectionAsync(c => c.UsingTransactionAsync(IsolationLevel.ReadCommitted, async t =>
            {
                await t.InsertAsync(table, new Person() { ID = 2, GroupID = 1, Name = "Bob" });
                return await t.CreateSimple($"select count(*) from {table}").ExecuteScalarAsync<int>(Token);
            }, Token), Token);

            fromString.Should().Be(1);
            fromConnection.Should().Be(2);
            ReadPersons(table).Select(x => x.Name).Should().Equal("Alice", "Bob");
        }

        [Fact]
        public async Task UsingTransactionAsync_FuncThrows_RollsBackAndThrowsTheSameExceptionAsync()
        {
            var table = CreatePersonTable();
            var exception = new InvalidOperationException("stop");

            Func<Task> act = () => Context.UsingTransactionAsync<int>(async t =>
            {
                await t.InsertAsync(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                throw exception;
            });

            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(exception);
            ReadPersons(table).Should().BeEmpty();
        }

        // An async lambda binds to the sync overloads with a result. The scope would end at its first await, so the
        // result is refused: the transaction is rolled back, and the message names the async method
        [Fact]
        public void UsingTransaction_AsyncDelegate_ThrowsAndRollsBack()
        {
            var table = CreatePersonTable();

            Action fromString = () => Context.UsingTransaction(async t =>
            {
                t.Insert(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                await Task.Yield();
                return 1;
            });
            Action fromConnection = () => Context.UsingConnection(c => c.UsingTransaction(async t =>
            {
                t.Insert(table, new Person() { ID = 2, GroupID = 1, Name = "Bob" });
                await Task.Yield();
            }));
            Action valueTask = () => Context.UsingTransaction(t =>
            {
                t.Insert(table, new Person() { ID = 3, GroupID = 1, Name = "Carol" });
                return new ValueTask<int>(1);
            });

            fromString.Should().Throw<InvalidOperationException>().WithMessage("UsingTransaction cannot run a delegate that returns a task*Use UsingTransactionAsync*");
            fromConnection.Should().Throw<InvalidOperationException>().WithMessage("UsingTransaction cannot run a delegate that returns a task*");
            valueTask.Should().Throw<InvalidOperationException>().WithMessage("UsingTransaction cannot run a delegate that returns a task*");
            ReadPersons(table).Should().BeEmpty();
        }

        [Fact]
        public void UsingConnection_AsyncDelegate_Throws()
        {
            Action asyncLambda = () => Context.UsingConnection(async c =>
            {
                await Task.Yield();
                return (int)c.Connection.State;
            });
            Action returnsATask = () => Context.UsingConnection(c => c.CreateSimple("select 1").ExecuteScalarAsync(Token));

            asyncLambda.Should().Throw<InvalidOperationException>().WithMessage("UsingConnection cannot run a delegate that returns a task*Use UsingConnectionAsync*");
            returnsATask.Should().Throw<InvalidOperationException>().WithMessage("UsingConnection cannot run a delegate that returns a task*");
        }

        [Fact]
        public async Task UsingConnectionAsync_CancelledToken_DoesNotRunTheActionAsync()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            bool ran = false;

            Func<Task> connection = () => Context.UsingConnectionAsync(c => { ran = true; return Task.CompletedTask; }, source.Token);
            Func<Task> transaction = () => Context.UsingTransactionAsync(t => { ran = true; return Task.CompletedTask; }, source.Token);
            Func<Task> withResult = () => Context.UsingTransactionAsync(t => { ran = true; return Task.FromResult(1); }, source.Token);

            await connection.Should().ThrowAsync<OperationCanceledException>();
            await transaction.Should().ThrowAsync<OperationCanceledException>();
            await withResult.Should().ThrowAsync<OperationCanceledException>();
            ran.Should().BeFalse();
        }

        // the token also cancels the commit: what the action did is rolled back
        [Fact]
        public async Task UsingTransactionAsync_TokenCancelledByTheAction_DoesNotCommitAsync()
        {
            var table = CreatePersonTable();
            using var source = new CancellationTokenSource();

            Func<Task> act = () => Context.UsingTransactionAsync(async t =>
            {
                await t.InsertAsync(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" });
                source.Cancel();
            }, source.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            ReadPersons(table).Should().BeEmpty();
        }

        [Fact]
        public async Task UsingTransactionAsync_WithToken_CommitsAsync()
        {
            var table = CreatePersonTable();

            await Context.UsingTransactionAsync(t => t.InsertAsync(table, new Person() { ID = 1, GroupID = 1, Name = "Alice" }), Token);
            await Context.UsingTransactionAsync(IsolationLevel.ReadCommitted, t => t.InsertAsync(table, new Person() { ID = 2, GroupID = 1, Name = "Bob" }), Token);
            await Context.UsingConnectionAsync(c => c.UsingTransactionAsync(async t =>
            {
                await t.InsertAsync(table, new Person() { ID = 3, GroupID = 1, Name = "Carol" });
            }, Token), Token);

            ReadPersons(table).Select(x => x.Name).Should().Equal("Alice", "Bob", "Carol");
        }

        #endregion

        #region I18: records, I17: the untyped first column

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQuery_PositionalRecord_ReadsTheRows(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var records = Database.Run(kind, x => x.CreateSimple($"select * from {table} order by {Q("ID")}").ExecuteQuery<PersonRecord>().ToArray());

            records.Should().Equal(ThreeRecords);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQuery_PositionalRecord_ReadsTheRowsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            var records = await Database.RunAsync(kind, x => x.CreateSimple($"select * from {table} order by {Q("ID")}")
                .ExecuteQueryAsync<PersonRecord>(cancellationToken: Token).ToListCompat(Token));

            records.Should().Equal(ThreeRecords);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryFirstColumn_Untyped_IsAGenericSequence(ContextKind kind)
        {
            var table = CreatePersonTable();
            InsertPersons(table, ThreePersons);

            // LINQ without Cast<object>(); a null in the database is null here
            var ages = Database.Run(kind, x => x.CreateSimple($"select {Q("Age")} from {table} order by {Q("ID")}").ExecuteQueryFirstColumn()
                .Select(age => age == null ? "null" : age.ToString()).ToArray());

            ages.Should().Equal("30", "40", "null");
        }

        #endregion

        #region I16: bulk copy of an async sequence

        private static async IAsyncEnumerable<Person> PersonsAsync(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= count; i++)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return new Person() { ID = i, GroupID = i % 3, Name = "Person " + i, Age = i % 2 == 0 ? null : (int?)i };
            }
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_AsyncSequence_CopiesTheRowsAsync(ContextKind kind)
        {
            var table = CreatePersonTable();

            await Database.RunAsync(kind, x => PersonsAsync(50).WriteToServerAsync(x, table, Token));

            var persons = ReadPersons(table);
            persons.Should().HaveCount(50);
            persons.Select(x => (x.ID, x.GroupID, x.Name, x.Age)).Should().Equal(
                Enumerable.Range(1, 50).Select(i => (i, i % 3, "Person " + i, i % 2 == 0 ? null : (int?)i)));
        }

        [Fact]
        public async Task WriteToServerAsync_AsyncQueryOfAnotherTable_CopiesTheRowsAsync()
        {
            var source = CreatePersonTable();
            var destination = CreatePersonTable();
            InsertPersons(source, ThreePersons);

            // the query keeps its own connection while the copy uses another one
            await Context.CreateSimple($"select * from {source}").ExecuteQueryAsync<Person>(cancellationToken: Token)
                .WriteToServerAsync(Context, destination, new BulkOptions(), Token, "ID", "GroupID", "Name", "Age");

            ReadPersons(destination).Should().BeEquivalentTo(ThreePersons, o => o.WithStrictOrdering());
        }

        #endregion

        #region I10: parameter names with letters of other alphabets

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_ParameterNamesWithNonAsciiLetters_AreFoundInTheText(ContextKind kind)
        {
            int german = Database.Run(kind, x => x.CreateMapped("select @Größe + 1", new { Größe = 41 }).ExecuteScalar<int>());
            string cyrillic = Database.Run(kind, x => x.CreateMapped("select @Имя", new { Имя = "имя" }).ExecuteScalar<string>());

            german.Should().Be(42);
            cyrillic.Should().Be("имя");
        }

        #endregion
    }
}
