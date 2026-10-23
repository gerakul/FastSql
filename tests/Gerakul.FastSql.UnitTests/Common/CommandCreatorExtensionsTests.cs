using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // U8. Every CommandCreatorExtensions overload of Gerakul.FastSql.Common, checked on the DbCommand it builds in a
    // capturing context (a scoped context over an unopened connection). Real execution is covered by the integration tests.
    // Concrete classes are in the provider folders.
    public abstract class CommandCreatorExtensionsTests<TSpec> where TSpec : ProviderSpec, new()
    {
        private class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        private class FieldAndProperty
        {
            public int Field;
            public string Property { get; set; }
        }

        // a command creator that is not a context: keeps the command getters it receives
        private class RecordingCommandCreator : ICommandCreator
        {
            public List<Func<ScopedContext, DbCommand>> Getters { get; } = new List<Func<ScopedContext, DbCommand>>();

            public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
            {
                Getters.Add(commandGetter);
                return null;
            }
        }

        private const int Timeout = 123;
        private const string NotMappedMessage = "Not all parameters has been mapped on settings (Parameter 'parameters')";

        private readonly ContextProvider provider;
        private readonly ICapturingContext context;
        private readonly int defaultTimeout;
        private readonly QueryOptions queryOptions = new QueryOptions(Timeout);
        private readonly Person person = new Person() { ID = 1, Name = "John", Age = 30 };
        private readonly FieldAndProperty fieldAndProperty = new FieldAndProperty() { Field = 7, Property = "p" };
        private readonly FieldSettings<Person>[] codeSettings = new[] { new FieldSettings<Person>("Code", typeof(int), x => x.ID * 10) };

        protected CommandCreatorExtensionsTests()
        {
            this.provider = Spec.CreateProvider();
            this.context = Spec.CreateCapturingContext(provider);

            using (var connection = Spec.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                this.defaultTimeout = command.CommandTimeout;
            }
        }

        protected TSpec Spec { get; } = new TSpec();

        // messages of the driver exceptions for executing a command on a closed connection
        protected abstract string ClosedConnectionMessage { get; }

        protected abstract string ClosedConnectionAsyncMessage { get; }

        private CommandTextGenerator Generator => provider.CommandTextGenerator;

        #region CreateSimple, CreateProcedureSimple

        [Fact]
        public void CreateSimple_TextAndArguments_BuildsTextCommand()
        {
            context.CreateSimple("select @p0, @p1", 1, "a");

            AssertCommand(context.LastCommand, "select @p0, @p1", CommandType.Text, defaultTimeout, ("@p0", 1), ("@p1", "a"));
        }

        [Fact]
        public void CreateSimple_QueryOptionsTextAndArguments_AppliesQueryOptions()
        {
            context.CreateSimple(queryOptions, "select @p0", 1);

            AssertCommand(context.LastCommand, "select @p0", CommandType.Text, Timeout, ("@p0", 1));
        }

        [Fact]
        public void CreateSimple_PrecompiledAndArguments_BuildsCompiledCommand()
        {
            context.CreateSimple(provider.CommandCompilator.CompileSimple("select @p0"), 1);

            AssertCommand(context.LastCommand, "select @p0", CommandType.Text, defaultTimeout, ("@p0", 1));
        }

        [Fact]
        public void CreateSimple_QueryOptionsPrecompiledAndArguments_AppliesQueryOptions()
        {
            context.CreateSimple(queryOptions, provider.CommandCompilator.CompileProcedureSimple("TestProc"), 1);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("@p0", 1));
        }

        [Fact]
        [Trait("Quirk", "Q60")]
        public void CreateSimple_SingleNullArgument_PassedAsNullArrayAndThrowsNullReferenceException()
        {
            Action act = () => context.CreateSimple("select @p0", null);

            act.Should().Throw<NullReferenceException>().WithMessage("Object reference not set to an instance of an object.");
        }

        [Fact]
        public void CreateSimple_SingleNullArgumentCastToObject_BecomesDBNull()
        {
            context.CreateSimple("select @p0", (object)null);

            ParametersOf(context.LastCommand).Should().Equal(("@p0", DBNull.Value));
        }

        [Fact]
        public void CreateProcedureSimple_NameAndParameters_AddsParametersAsIs()
        {
            var companyId = Spec.CreateParameter("@CompanyID", 1);
            var age = Spec.CreateParameter("@Age", 40);

            context.CreateProcedureSimple("TestProc", companyId, age);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, defaultTimeout, ("@CompanyID", 1), ("@Age", 40));
            context.LastCommand.Parameters[0].Should().BeSameAs(companyId);
            context.LastCommand.Parameters[1].Should().BeSameAs(age);
        }

        [Fact]
        public void CreateProcedureSimple_QueryOptionsNameAndParameters_AppliesQueryOptions()
        {
            context.CreateProcedureSimple(queryOptions, "TestProc", Spec.CreateParameter("@CompanyID", 1));

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("@CompanyID", 1));
        }

        #endregion

        #region CreateMapped

        [Fact]
        public void CreateMapped_TextAndValue_MapsParametersFromText()
        {
            context.CreateMapped("select @ID, @Name", person);

            AssertCommand(context.LastCommand, "select @ID, @Name", CommandType.Text, defaultTimeout, ("@ID", 1), ("@Name", "John"));
        }

        [Fact]
        public void CreateMapped_TextValueAndQueryOptions_AppliesQueryOptions()
        {
            context.CreateMapped("select @ID", person, queryOptions);

            AssertCommand(context.LastCommand, "select @ID", CommandType.Text, Timeout, ("@ID", 1));
        }

        [Fact]
        public void CreateMapped_TextDefaultFromTypeOption_UsesFieldsAndProperties()
        {
            context.CreateMapped("select @Field, @Property", fieldAndProperty);

            ParametersOf(context.LastCommand).Should().Equal(("@Field", 7), ("@Property", "p"));
        }

        [Fact]
        public void CreateMapped_TextAndFromTypeOption_UsesSelectedMembersOnly()
        {
            context.CreateMapped("select @Field", fieldAndProperty, fromTypeOption: FromTypeOption.PublicField);

            ParametersOf(context.LastCommand).Should().Equal(("@Field", 7));

            Action act = () => context.CreateMapped("select @Field", fieldAndProperty, fromTypeOption: FromTypeOption.PublicProperty);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        [Fact]
        public void CreateMapped_CaseSensitiveQueryOptions_DifferentCaseIsNotMapped()
        {
            context.CreateMapped("select @id", person);

            ParametersOf(context.LastCommand).Should().Equal(("@id", 1));

            Action act = () => context.CreateMapped("select @id", person, new QueryOptions(caseSensitiveParamsMatching: true));

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        [Fact]
        public void CreateMapped_PrecompiledAndValue_BuildsCompiledCommand()
        {
            var compiled = provider.CommandCompilator.CompileMapped<Person>("select @ID");

            context.CreateMapped(compiled, person);

            AssertCommand(context.LastCommand, "select @ID", CommandType.Text, defaultTimeout, ("@ID", 1));

            context.CreateMapped(compiled, person, queryOptions);

            AssertCommand(context.LastCommand, "select @ID", CommandType.Text, Timeout, ("@ID", 1));
        }

        [Fact]
        public void CreateMapped_TextNamesAndValue_UsesGivenNames()
        {
            context.CreateMapped("select @ID, @Name", new[] { "@Name" }, person);

            AssertCommand(context.LastCommand, "select @ID, @Name", CommandType.Text, defaultTimeout, ("@Name", "John"));
        }

        [Fact]
        public void CreateMapped_TextNamesDefaultFromTypeOption_UsesFieldsAndProperties()
        {
            context.CreateMapped("select @Field, @Property", new[] { "@Field", "@Property" }, fieldAndProperty);

            ParametersOf(context.LastCommand).Should().Equal(("@Field", 7), ("@Property", "p"));
        }

        [Fact]
        public void CreateMapped_TextNamesValueQueryOptionsAndFromTypeOption_ForwardsOptions()
        {
            context.CreateMapped("select @Field", new[] { "@Field" }, fieldAndProperty, queryOptions, FromTypeOption.PublicField);

            AssertCommand(context.LastCommand, "select @Field", CommandType.Text, Timeout, ("@Field", 7));

            Action act = () => context.CreateMapped("select @Field", new[] { "@Field" }, fieldAndProperty, queryOptions, FromTypeOption.PublicProperty);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        [Fact]
        public void CreateMapped_TextSettingsAndValue_UsesSettings()
        {
            context.CreateMapped("select @Code", codeSettings, person);

            AssertCommand(context.LastCommand, "select @Code", CommandType.Text, defaultTimeout, ("@Code", 10));

            context.CreateMapped("select @Code", codeSettings, person, queryOptions);

            AssertCommand(context.LastCommand, "select @Code", CommandType.Text, Timeout, ("@Code", 10));
        }

        [Fact]
        public void CreateMapped_TextNamesSettingsAndValue_UsesNamesAndSettings()
        {
            context.CreateMapped("select @Code", new[] { "Code" }, codeSettings, person);

            AssertCommand(context.LastCommand, "select @Code", CommandType.Text, defaultTimeout, ("Code", 10));

            context.CreateMapped("select @Code", new[] { "Code" }, codeSettings, person, queryOptions);

            AssertCommand(context.LastCommand, "select @Code", CommandType.Text, Timeout, ("Code", 10));
        }

        #endregion

        #region CreateProcedure

        [Fact]
        public void CreateProcedure_NameAndValue_ParametersFromAllMembers()
        {
            context.CreateProcedure("TestProc", person);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, defaultTimeout, ("@ID", 1), ("@Name", "John"), ("@Age", 30));
        }

        [Fact]
        public void CreateProcedure_DefaultFromTypeOption_UsesFieldsAndProperties()
        {
            context.CreateProcedure("TestProc", fieldAndProperty);

            ParametersOf(context.LastCommand).Should().Equal(("@Field", 7), ("@Property", "p"));
        }

        [Fact]
        public void CreateProcedure_NameValueQueryOptionsAndFromTypeOption_ForwardsOptions()
        {
            context.CreateProcedure("TestProc", fieldAndProperty, queryOptions, FromTypeOption.PublicProperty);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("@Property", "p"));
        }

        [Fact]
        public void CreateProcedure_NamesAndValue_OnlyGivenParameters()
        {
            context.CreateProcedure("TestProc", new[] { "@ID", "@Age" }, person);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, defaultTimeout, ("@ID", 1), ("@Age", 30));
        }

        [Fact]
        public void CreateProcedure_NamesDefaultFromTypeOption_UsesFieldsAndProperties()
        {
            context.CreateProcedure("TestProc", new[] { "@Field", "@Property" }, fieldAndProperty);

            ParametersOf(context.LastCommand).Should().Equal(("@Field", 7), ("@Property", "p"));
        }

        [Fact]
        public void CreateProcedure_NamesValueQueryOptionsAndFromTypeOption_ForwardsOptions()
        {
            context.CreateProcedure("TestProc", new[] { "@Field" }, fieldAndProperty, queryOptions, FromTypeOption.PublicField);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("@Field", 7));

            Action act = () => context.CreateProcedure("TestProc", new[] { "@Field" }, fieldAndProperty, queryOptions, FromTypeOption.PublicProperty);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        [Fact]
        public void CreateProcedure_SettingsAndValue_ParametersFromSettings()
        {
            context.CreateProcedure("TestProc", codeSettings, person);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, defaultTimeout, ("@Code", 10));

            context.CreateProcedure("TestProc", codeSettings, person, queryOptions);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("@Code", 10));
        }

        [Fact]
        public void CreateProcedure_NamesSettingsAndValue_UsesNamesAndSettings()
        {
            context.CreateProcedure("TestProc", new[] { "Code" }, codeSettings, person);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, defaultTimeout, ("Code", 10));

            context.CreateProcedure("TestProc", new[] { "Code" }, codeSettings, person, queryOptions);

            AssertCommand(context.LastCommand, "TestProc", CommandType.StoredProcedure, Timeout, ("Code", 10));
        }

        #endregion

        #region CreateInsert, CreateInsertWithOutput, CreateUpdate, CreateDelete, CreateMerge

        [Fact]
        public void CreateInsert_IgnoreFields_InsertsOtherMembers()
        {
            context.CreateInsert("Person", person, "ID");

            AssertCommand(context.LastCommand, Generator.Insert("Person", "Name", "Age"), CommandType.Text, defaultTimeout, ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsert_QueryOptionsAndIgnoreFields_AppliesQueryOptions()
        {
            context.CreateInsert("Person", person, queryOptions, "ID");

            AssertCommand(context.LastCommand, Generator.Insert("Person", "Name", "Age"), CommandType.Text, Timeout, ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertWithOutput_OutputFields_InsertsAllMembers()
        {
            context.CreateInsertWithOutput("Person", person, "ID");

            AssertCommand(context.LastCommand, Generator.InsertWithOutput("Person", new[] { "ID", "Name", "Age" }, "ID"), CommandType.Text, defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertWithOutput_NoOutputFields_PlainInsert()
        {
            context.CreateInsertWithOutput("Person", person);

            AssertCommand(context.LastCommand, Generator.Insert("Person", "ID", "Name", "Age"), CommandType.Text, defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertWithOutput_QueryOptionsAndOutputFields_AppliesQueryOptions()
        {
            context.CreateInsertWithOutput("Person", person, queryOptions, "ID");

            AssertCommand(context.LastCommand, Generator.InsertWithOutput("Person", new[] { "ID", "Name", "Age" }, "ID"), CommandType.Text, Timeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertWithOutput_IgnoreFieldsAndOutputFields_InsertsOtherMembers()
        {
            context.CreateInsertWithOutput("Person", person, new List<string>() { "ID" }, "ID", "Name");

            AssertCommand(context.LastCommand, Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID", "Name"), CommandType.Text, defaultTimeout,
                ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertWithOutput_QueryOptionsIgnoreFieldsAndOutputFields_AppliesQueryOptions()
        {
            context.CreateInsertWithOutput("Person", person, queryOptions, new List<string>() { "ID" }, "ID");

            AssertCommand(context.LastCommand, Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID"), CommandType.Text, Timeout,
                ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateUpdate_KeyFields_UpdatesOtherMembers()
        {
            context.CreateUpdate("Person", person, "ID");

            AssertCommand(context.LastCommand, Generator.Update("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateUpdate_QueryOptionsAndKeyFields_AppliesQueryOptions()
        {
            context.CreateUpdate("Person", person, queryOptions, "ID");

            AssertCommand(context.LastCommand, Generator.Update("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, Timeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateDelete_KeyFields_DeletesByKeys()
        {
            context.CreateDelete("Person", person, "ID");

            AssertCommand(context.LastCommand, Generator.Delete("Person", new[] { "ID" }), CommandType.Text, defaultTimeout, ("ID", 1));
        }

        [Fact]
        public void CreateDelete_NoKeyFields_DeletesByAllMembers()
        {
            // the README shortcut: context.Delete("Employee", new { ID = 2 })
            context.CreateDelete("Employee", new { ID = 2 });

            AssertCommand(context.LastCommand, Generator.Delete("Employee", new[] { "ID" }), CommandType.Text, defaultTimeout, ("ID", 2));
        }

        [Fact]
        public void CreateDelete_QueryOptionsAndKeyFields_AppliesQueryOptions()
        {
            context.CreateDelete("Person", person, queryOptions, "ID");

            AssertCommand(context.LastCommand, Generator.Delete("Person", new[] { "ID" }), CommandType.Text, Timeout, ("ID", 1));
        }

        [Fact]
        public void CreateMerge_KeyFields_MergesOnKeys()
        {
            context.CreateMerge("Person", person, "ID");

            AssertCommand(context.LastCommand, Generator.Merge("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateMerge_QueryOptionsAndKeyFields_AppliesQueryOptions()
        {
            context.CreateMerge("Person", person, queryOptions, "ID");

            AssertCommand(context.LastCommand, Generator.Merge("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, Timeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        #endregion

        #region Insert, Update, Delete, Merge and async twins

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public void Insert_UnopenedConnection_BuildsCommandThenThrows(bool withQueryOptions)
        {
            Action act = withQueryOptions
                ? () => context.Insert("Person", person, queryOptions, "ID")
                : () => context.Insert("Person", person, "ID");

            act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage);
            AssertCommand(context.LastCommand, Generator.Insert("Person", "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("Name", "John"), ("Age", 30));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InsertAsync_UnopenedConnection_BuildsCommandThenThrowsAsync(bool withQueryOptions)
        {
            Func<Task> act = withQueryOptions
                ? () => context.InsertAsync("Person", person, queryOptions, "ID")
                : () => context.InsertAsync("Person", person, "ID");

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionAsyncMessage);
            AssertCommand(context.LastCommand, Generator.Insert("Person", "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("Name", "John"), ("Age", 30));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public void Update_UnopenedConnection_BuildsCommandThenThrows(bool withQueryOptions)
        {
            Action act = withQueryOptions
                ? () => context.Update("Person", person, queryOptions, "ID")
                : () => context.Update("Person", person, "ID");

            act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage);
            AssertCommand(context.LastCommand, Generator.Update("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task UpdateAsync_UnopenedConnection_BuildsCommandThenThrowsAsync(bool withQueryOptions)
        {
            Func<Task> act = withQueryOptions
                ? () => context.UpdateAsync("Person", person, queryOptions, "ID")
                : () => context.UpdateAsync("Person", person, "ID");

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionAsyncMessage);
            AssertCommand(context.LastCommand, Generator.Update("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public void Delete_UnopenedConnection_BuildsCommandThenThrows(bool withQueryOptions)
        {
            Action act = withQueryOptions
                ? () => context.Delete("Person", person, queryOptions, "ID")
                : () => context.Delete("Person", person, "ID");

            act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage);
            AssertCommand(context.LastCommand, Generator.Delete("Person", new[] { "ID" }), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeleteAsync_UnopenedConnection_BuildsCommandThenThrowsAsync(bool withQueryOptions)
        {
            Func<Task> act = withQueryOptions
                ? () => context.DeleteAsync("Person", person, queryOptions, "ID")
                : () => context.DeleteAsync("Person", person, "ID");

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionAsyncMessage);
            AssertCommand(context.LastCommand, Generator.Delete("Person", new[] { "ID" }), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public void Merge_UnopenedConnection_BuildsCommandThenThrows(bool withQueryOptions)
        {
            Action act = withQueryOptions
                ? () => context.Merge("Person", person, queryOptions, "ID")
                : () => context.Merge("Person", person, "ID");

            act.Should().Throw<InvalidOperationException>().WithMessage(ClosedConnectionMessage);
            AssertCommand(context.LastCommand, Generator.Merge("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Theory]
        [Trait("UpgradeSensitive", "driver message for a closed connection")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MergeAsync_UnopenedConnection_BuildsCommandThenThrowsAsync(bool withQueryOptions)
        {
            Func<Task> act = withQueryOptions
                ? () => context.MergeAsync("Person", person, queryOptions, "ID")
                : () => context.MergeAsync("Person", person, "ID");

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ClosedConnectionAsyncMessage);
            AssertCommand(context.LastCommand, Generator.Merge("Person", new[] { "ID" }, "Name", "Age"), CommandType.Text, withQueryOptions ? Timeout : defaultTimeout,
                ("ID", 1), ("Name", "John"), ("Age", 30));
        }

        #endregion

        #region Custom extensions through ICommandCreator.Set, scoped contexts

        [Fact]
        public void Set_ReadmeCustomExtensionOnScopedContext_BuildsCommand()
        {
            var wrapped = CreateSelectTop10Employees(context);

            wrapped.Should().NotBeNull();
            AssertCommand(context.LastCommand, "select top 10 * from dbo.Employee", CommandType.Text, 1000);
        }

        [Fact]
        public void Set_ScopedContext_CallsGetterOnceImmediatelyWithContext()
        {
            var calls = new List<ScopedContext>();

            context.Set(x =>
            {
                calls.Add(x);
                return x.CreateCommand("select 1");
            });

            calls.Should().ContainSingle().Which.Should().BeSameAs(context.Context);
            context.Commands.Should().ContainSingle();
        }

        [Fact]
        public void Set_ConnectionAndTransactionContexts_CallGetterImmediatelyWithContext()
        {
            // neither context needs an open connection or a real transaction until a command is executed
            var contexts = new ScopedContext[] { provider.CreateContext(Spec.CreateConnection()), provider.CreateContext((DbTransaction)null) };

            foreach (var scopedContext in contexts)
            {
                var calls = new List<ScopedContext>();

                var wrapped = scopedContext.Set(x =>
                {
                    calls.Add(x);
                    return null;
                });

                wrapped.Should().NotBeNull();
                calls.Should().ContainSingle().Which.Should().BeSameAs(scopedContext);
            }
        }

        [Fact]
        public void Set_ConnectionStringContext_DoesNotCallGetterBeforeExecution()
        {
            var connectionStringContext = provider.CreateContext("unused");
            var calls = 0;

            var wrapped = connectionStringContext.Set(x =>
            {
                calls++;
                return null;
            });
            CreateSelectTop10Employees(connectionStringContext);
            connectionStringContext.CreateMapped("select @ID", person);

            wrapped.Should().NotBeNull();
            calls.Should().Be(0);
        }

        [Fact]
        public void Set_EachCallOnContext_ReturnsNewWrappedCommand()
        {
            var connectionStringContext = provider.CreateContext("unused");

            connectionStringContext.CreateSimple("select 1").Should().NotBeSameAs(connectionStringContext.CreateSimple("select 1"));
            context.CreateSimple("select 1").Should().NotBeSameAs(context.CreateSimple("select 1"));
        }

        [Fact]
        public void CreateInsert_CustomCommandCreator_PassesCommandGetterToSet()
        {
            var creator = new RecordingCommandCreator();

            var wrapped = creator.CreateInsert("Person", person, "ID");

            wrapped.Should().BeNull();
            creator.Getters.Should().ContainSingle();

            creator.Getters[0](context.Context);

            context.LastCommand.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
        }

        [Fact]
        [Trait("Quirk", "Q15")]
        public void CreateMapped_ScopedContext_ReadsMemberValuesWhenCreated()
        {
            var value = new Person() { ID = 1, Name = "before" };

            context.CreateMapped("select @ID, @Name", value);
            context.CreateInsert("Person", value, "ID");
            value.Name = "after";

            context.Commands.Should().HaveCount(2);
            ParametersOf(context.Commands[0]).Should().Equal(("@ID", 1), ("@Name", "before"));
            ParametersOf(context.Commands[1]).Should().Equal(("Name", "before"), ("Age", DBNull.Value));
        }

        #endregion

        // the README "Extensibility" example, written as a plain method: a custom command for every context kind
        private static IWrappedCommand CreateSelectTop10Employees(ICommandCreator creator)
        {
            return creator.Set(c =>
            {
                var cmd = c.CreateCommand("select top 10 * from dbo.Employee");
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 1000;
                return cmd;
            });
        }

        private static void AssertCommand(DbCommand command, string commandText, CommandType commandType, int commandTimeout,
            params (string Name, object Value)[] parameters)
        {
            command.CommandText.Should().Be(commandText);
            command.CommandType.Should().Be(commandType);
            command.CommandTimeout.Should().Be(commandTimeout);
            ParametersOf(command).Should().Equal(parameters);
        }

        private static List<(string Name, object Value)> ParametersOf(DbCommand command)
        {
            return command.Parameters.Cast<DbParameter>().Select(x => (x.ParameterName, x.Value)).ToList();
        }
    }
}
