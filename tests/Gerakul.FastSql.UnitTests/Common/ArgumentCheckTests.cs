using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // I9: a null argument is reported at the call, with its name, also by methods that do their work later
    // (lazy queries, async methods, commands of connection string contexts). Until phase 4 such calls failed
    // somewhere inside, mostly with a NullReferenceException when the command ran.
    public class ArgumentCheckTests
    {
        private class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        private static readonly SqlServerSpec spec = new SqlServerSpec();
        private static readonly ContextProvider provider = spec.CreateProvider();
        private static readonly Person person = new Person() { ID = 1, Name = "John" };
        private static readonly FieldSettings<Person>[] settings = FieldSettings.FromType<Person>();

        // a context that would connect only when a command runs: the checks must not wait for that
        private static ConnectionStringContext StringContext => provider.CreateContext("Server=unreachable.invalid;Connect Timeout=1");

        private static ICapturingContext ScopedContext => spec.CreateCapturingContext(provider);

        private static DbDataReader Reader => new ReaderTable().Column<int>("ID").Row(1).CreateReader();

        private static async IAsyncEnumerable<Person> AsyncPersons()
        {
            await Task.Yield();
            yield return person;
        }

        public static TheoryData<string, string, Action> Calls()
        {
            var data = new TheoryData<string, string, Action>();

            void Add(string name, string parameter, Action call)
            {
                data.Add(name, parameter, call);
            }

            // contexts
            Add("CreateContext(string)", "connectionString", () => provider.CreateContext((string)null));
            Add("CreateContext(DbConnection)", "connection", () => provider.CreateContext((DbConnection)null));
            Add("Set", "commandGetter", () => StringContext.Set(null));
            Add("UsingConnection", "action", () => StringContext.UsingConnection((Action<ConnectionContext>)null));
            Add("UsingConnection<TResult>", "func", () => StringContext.UsingConnection((Func<ConnectionContext, int>)null));
            Add("UsingConnectionAsync", "action", () => StringContext.UsingConnectionAsync((Func<ConnectionContext, Task>)null));
            Add("UsingConnectionAsync(token)", "action", () => StringContext.UsingConnectionAsync((Func<ConnectionContext, Task>)null, CancellationToken.None));
            Add("UsingConnectionAsync<TResult>", "func", () => StringContext.UsingConnectionAsync((Func<ConnectionContext, Task<int>>)null));
            Add("UsingTransaction", "action", () => StringContext.UsingTransaction((Action<TransactionContext>)null));
            Add("UsingTransaction(level)", "action", () => StringContext.UsingTransaction(IsolationLevel.ReadCommitted, (Action<TransactionContext>)null));
            Add("UsingTransaction<TResult>", "func", () => StringContext.UsingTransaction((Func<TransactionContext, int>)null));
            Add("UsingTransactionAsync", "action", () => StringContext.UsingTransactionAsync((Func<TransactionContext, Task>)null));
            Add("UsingTransactionAsync(token)", "action", () => StringContext.UsingTransactionAsync((Func<TransactionContext, Task>)null, CancellationToken.None));
            Add("UsingTransactionAsync<TResult>", "func", () => StringContext.UsingTransactionAsync((Func<TransactionContext, Task<int>>)null));
            Add("ConnectionContext.UsingTransaction", "action", () => ((ConnectionContext)ScopedContext.Context).UsingTransaction((Action<TransactionContext>)null));
            Add("ConnectionContext.UsingTransaction<TResult>", "func", () => ((ConnectionContext)ScopedContext.Context).UsingTransaction((Func<TransactionContext, int>)null));
            Add("ConnectionContext.UsingTransactionAsync", "action", () => ((ConnectionContext)ScopedContext.Context).UsingTransactionAsync((Func<TransactionContext, Task>)null));
            Add("ConnectionContext.UsingTransactionAsync<TResult>", "func", () => ((ConnectionContext)ScopedContext.Context).UsingTransactionAsync((Func<TransactionContext, Task<int>>)null));

            // creating commands: connection string contexts build the command only when it runs
            foreach (var (kind, context) in new (string, ICommandCreator)[] { ("string context", StringContext), ("scoped context", ScopedContext) })
            {
                Add($"CreateSimple, {kind}", "commandText", () => context.CreateSimple((string)null));
                Add($"CreateSimple(options), {kind}", "commandText", () => context.CreateSimple(new QueryOptions(), (string)null));
                Add($"CreateSimple(precompiled), {kind}", "precompiledCommand", () => context.CreateSimple((SimpleCommand)null));
                Add($"CreateProcedureSimple, {kind}", "name", () => context.CreateProcedureSimple(null));
                Add($"CreateMapped(text), {kind}", "commandText", () => context.CreateMapped((string)null, person));
                Add($"CreateMapped(text, names), {kind}", "commandText", () => context.CreateMapped(null, new[] { "@ID" }, person));
                Add($"CreateMapped(text, settings), {kind}", "commandText", () => context.CreateMapped(null, settings, person));
                Add($"CreateMapped(text, null settings), {kind}", "settings", () => context.CreateMapped("select 1", (IList<FieldSettings<Person>>)null, person));
                Add($"CreateMapped(precompiled), {kind}", "precompiledCommand", () => context.CreateMapped((MappedCommand<Person>)null, person));
                Add($"CreateProcedure, {kind}", "name", () => context.CreateProcedure(null, person));
                Add($"CreateProcedure(null settings), {kind}", "settings", () => context.CreateProcedure("P", (IList<FieldSettings<Person>>)null, person));
                Add($"CreateInsert, {kind}", "tableName", () => context.CreateInsert(null, person));
                Add($"CreateInsertWithOutput, {kind}", "tableName", () => context.CreateInsertWithOutput(null, person, "ID"));
                Add($"CreateInsertAndGetID, {kind}", "tableName", () => context.CreateInsertAndGetID(null, person, "ID"));
                Add($"CreateUpdate, {kind}", "tableName", () => context.CreateUpdate(null, person, "ID"));
                Add($"CreateDelete, {kind}", "tableName", () => context.CreateDelete(null, person, "ID"));
                Add($"CreateMerge, {kind}", "tableName", () => context.CreateMerge(null, person, "ID"));
                Add($"Insert, {kind}", "tableName", () => context.Insert(null, person));
                Add($"UpdateAsync, {kind}", "tableName", () => context.UpdateAsync(null, person, "ID"));
            }

            Add("CreateSimple, null creator", "creator", () => ((ICommandCreator)null).CreateSimple("select 1"));
            Add("CreateMapped, null creator", "creator", () => ((ICommandCreator)null).CreateMapped("select 1", person));
            Add("CreateInsert, null creator", "creator", () => ((ICommandCreator)null).CreateInsert("Person", person));
            Add("CreateInsertAndGetID, null creator", "creator", () => ((ICommandCreator)null).CreateInsertAndGetID("Person", person));

            // compiling commands
            var compilator = provider.CommandCompilator;
            Add("CompileSimple", "commandText", () => compilator.CompileSimple(null));
            Add("CompileProcedureSimple", "commandText", () => compilator.CompileProcedureSimple(null));
            Add("CompileMapped", "commandText", () => compilator.CompileMapped<Person>(null));
            Add("CompileMapped(null settings)", "settings", () => compilator.CompileMapped("select 1", (IList<FieldSettings<Person>>)null));
            Add("CompileProcedure", "commandText", () => compilator.CompileProcedure<Person>(null));
            Add("CompileInsert", "tableName", () => compilator.CompileInsert<Person>(null));
            Add("CompileInsert(null settings)", "settings", () => compilator.CompileInsert("Person", (IList<FieldSettings<Person>>)null));
            Add("CompileInsertAndGetID", "tableName", () => compilator.CompileInsertAndGetID<Person>(null));
            Add("CompileInsertAndGetID(null settings)", "settings", () => compilator.CompileInsertAndGetID("Person", (IList<FieldSettings<Person>>)null));
            Add("CompileInsertWithOutput", "tableName", () => compilator.CompileInsertWithOutput(null, settings, null, "ID"));
            Add("CompileInsertWithOutput(null output fields)", "outputFields", () => compilator.CompileInsertWithOutput("Person", settings, null, null));
            Add("CompileUpdate", "tableName", () => compilator.CompileUpdate<Person>((string)null, "ID"));
            Add("CompileDelete", "tableName", () => compilator.CompileDelete<Person>((string)null, "ID"));
            Add("CompileMerge", "tableName", () => compilator.CompileMerge<Person>((string)null, "ID"));

            // executing through IWrappedCommand
            Add("UseReader, string context", "action", () => StringContext.CreateSimple("select 1").UseReader(null));
            Add("UseReader, scoped context", "action", () => ScopedContext.CreateSimple("select 1").UseReader(null));
            Add("UseReaderAsync, string context", "action", () => StringContext.CreateSimple("select 1").UseReaderAsync(null));
            Add("UseReaderAsync, scoped context", "action", () => ScopedContext.CreateSimple("select 1").UseReaderAsync(CancellationToken.None, null));
            Add("UseReaderAsync, null command", "cmd", () => ((IWrappedCommand)null).UseReaderAsync(x => Task.CompletedTask));
            Add("ExecuteScalar<T>, null command", "cmd", () => ((IWrappedCommand)null).ExecuteScalar<int>());
            Add("ExecuteScalarAsync<T>, null command", "cmd", () => ((IWrappedCommand)null).ExecuteScalarAsync<int>());
            Add("IWrappedCommand.WriteToServer, null command", "cmd", () => ((IWrappedCommand)null).WriteToServer(StringContext, "T"));
            Add("IWrappedCommand.WriteToServer, null context", "context", () => StringContext.CreateSimple("select 1").WriteToServer(null, "T"));
            Add("IWrappedCommand.WriteToServer, null table", "destinationTable", () => StringContext.CreateSimple("select 1").WriteToServer(StringContext, null));
            Add("IWrappedCommand.WriteToServerAsync, null context", "context", () => StringContext.CreateSimple("select 1").WriteToServerAsync(null, "T"));
            Add("IWrappedCommand.WriteToServerAsync(token), null table", "destinationTable",
                () => StringContext.CreateSimple("select 1").WriteToServerAsync(StringContext, null, CancellationToken.None));

            // readers
            Add("ReadAll", "reader", () => ((IDataReader)null).ReadAll());
            Add("ReadAllAsync", "reader", () => ((IDataReader)null).ReadAllAsync());
            Add("ReadAll<T>", "reader", () => ((IDataReader)null).ReadAll<Person>());
            Add("ReadAllAsync<T>", "reader", () => ((IDataReader)null).ReadAllAsync<Person>(new ReadOptions()));
            Add("ReadAllAnonymous", "reader", () => ((IDataReader)null).ReadAllAnonymous(person));
            Add("ReadAllAnonymousAsync", "reader", () => ((IDataReader)null).ReadAllAnonymousAsync(person));
            Add("ReadAllFirstColumn", "reader", () => ((IDataReader)null).ReadAllFirstColumn());
            Add("ReadAllFirstColumn<T>", "reader", () => ((IDataReader)null).ReadAllFirstColumn<int>());
            Add("ReadAllFirstColumnAsync", "reader", () => ((IDataReader)null).ReadAllFirstColumnAsync());
            Add("ReadAllFirstColumnAsync<T>", "reader", () => ((IDataReader)null).ReadAllFirstColumnAsync<int>());
            Add("GetColumnNames", "reader", () => ((IDataReader)null).GetColumnNames());
            Add("DbDataReader.WriteToServer, null reader", "reader", () => ((DbDataReader)null).WriteToServer(StringContext, "T"));
            Add("DbDataReader.WriteToServer, null context", "context", () => Reader.WriteToServer(null, "T"));
            Add("DbDataReader.WriteToServer, null table", "destinationTable", () => Reader.WriteToServer(StringContext, null));
            Add("DbDataReader.WriteToServerAsync, null context", "context", () => Reader.WriteToServerAsync(null, "T"));
            Add("DbDataReader.WriteToServerAsync(token), null table", "destinationTable", () => Reader.WriteToServerAsync(StringContext, null, CancellationToken.None));

            // raw commands
            Add("DbCommand.ExecuteQuery", "cmd", () => ((DbCommand)null).ExecuteQuery());
            Add("DbCommand.ExecuteQuery<T>", "cmd", () => ((DbCommand)null).ExecuteQuery<Person>());
            Add("DbCommand.ExecuteQueryAnonymous", "cmd", () => ((DbCommand)null).ExecuteQueryAnonymous(person));
            Add("DbCommand.ExecuteQueryFirstColumn", "cmd", () => ((DbCommand)null).ExecuteQueryFirstColumn());
            Add("DbCommand.ExecuteQueryFirstColumn<T>", "cmd", () => ((DbCommand)null).ExecuteQueryFirstColumn<int>());
            Add("DbCommand.ExecuteQueryAsync", "cmd", () => ((DbCommand)null).ExecuteQueryAsync());
            Add("DbCommand.ExecuteQueryAsync<T>", "cmd", () => ((DbCommand)null).ExecuteQueryAsync<Person>());
            Add("DbCommand.ExecuteQueryAnonymousAsync", "cmd", () => ((DbCommand)null).ExecuteQueryAnonymousAsync(person));
            Add("DbCommand.ExecuteQueryFirstColumnAsync", "cmd", () => ((DbCommand)null).ExecuteQueryFirstColumnAsync());
            Add("DbCommand.ExecuteQueryFirstColumnAsync<T>", "cmd", () => ((DbCommand)null).ExecuteQueryFirstColumnAsync<int>());

            // sequences of objects
            Add("ToDataReader", "values", () => ((IEnumerable<Person>)null).ToDataReader());
            Add("ToDataReader, async", "values", () => ((IAsyncEnumerable<Person>)null).ToDataReader());
            Add("ToDataReader(null settings)", "settings", () => new[] { person }.ToDataReader((IEnumerable<FieldSettings<Person>>)null));
            Add("new UniversalDataReader(null values)", "values", () => new UniversalDataReader<Person>((IEnumerable<Person>)null, settings, true));
            Add("IEnumerable.WriteToServer, null values", "values", () => ((IEnumerable<Person>)null).WriteToServer(StringContext, "T"));
            Add("IEnumerable.WriteToServer, null context", "context", () => new[] { person }.WriteToServer(null, "T"));
            Add("IEnumerable.WriteToServer, null table", "destinationTable", () => new[] { person }.WriteToServer(StringContext, null));
            Add("IEnumerable.WriteToServerAsync, null values", "values", () => ((IEnumerable<Person>)null).WriteToServerAsync(StringContext, "T"));
            Add("IEnumerable.WriteToServerAsync, null context", "context", () => new[] { person }.WriteToServerAsync(null, "T"));
            Add("IEnumerable.WriteToServerAsync(token), null table", "destinationTable", () => new[] { person }.WriteToServerAsync(StringContext, null, CancellationToken.None));
            Add("IAsyncEnumerable.WriteToServerAsync, null values", "values", () => ((IAsyncEnumerable<Person>)null).WriteToServerAsync(StringContext, "T"));
            Add("IAsyncEnumerable.WriteToServerAsync, null context", "context", () => AsyncPersons().WriteToServerAsync(null, "T"));
            Add("IAsyncEnumerable.WriteToServerAsync, null table", "destinationTable", () => AsyncPersons().WriteToServerAsync(StringContext, null));

            // field settings
            Add("FieldSettings(null name)", "name", () => new FieldSettings<Person>(null, typeof(int), x => x.ID));
            Add("FieldSettings(null getter)", "getter", () => new FieldSettings<Person>("ID", typeof(int), null));
            Add("FieldSettings(null is null getter)", "isNullGetter", () => new FieldSettings<Person>("ID", typeof(int), x => x.ID, null));
            Add("GetNames", "values", () => ((IEnumerable<FieldSettings<Person>>)null).GetNames());

            return data;
        }

        [Theory]
        [MemberData(nameof(Calls))]
        public void NullArgument_ThrowsArgumentNullExceptionAtTheCall(string name, string parameter, Action call)
        {
            call.Should().ThrowExactly<ArgumentNullException>(name).Which.ParamName.Should().Be(parameter);
        }

        // what stays as it was
        [Fact]
        public void CreateMapped_NullValue_IsNotChecked()
        {
            var context = ScopedContext;
            var single = new[] { new FieldSettings<string>("Name") };

            context.CreateMapped("select @Name", single, (string)null);

            context.LastCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        }

        [Fact]
        [Trait("Quirk", "Q60")]
        public void CreateSimple_NullParameterArray_StillThrowsNullReferenceException()
        {
            Action act = () => ScopedContext.CreateSimple("select 1", null);

            act.Should().ThrowExactly<NullReferenceException>();
        }

        [Fact]
        public void GetFieldSettings_NullSequence_StillWorks()
        {
            ((IEnumerable<Person>)null).GetFieldSettings().GetNames().Should().Equal("ID", "Name");
        }
    }
}
