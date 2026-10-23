using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // U7. Each compiled command is turned into a DbCommand through CreateSimple / CreateMapped on a capturing context.
    // Concrete classes are in the provider folders.
    public abstract class CommandCompilatorTests<TSpec> where TSpec : ProviderSpec, new()
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

        private class WithParameter
        {
            public int ID { get; set; }
            public DbParameter Result { get; set; }
        }

        private const string MappedText = "select * from Person where ID = @ID and Name = @Name";
        private const string NotMappedMessage = "Not all parameters has been mapped on settings (Parameter 'parameters')";

        private readonly ContextProvider provider;
        private readonly ICapturingContext context;
        private readonly Person person = new Person() { ID = 1, Name = "John", Age = 30 };
        private readonly FieldAndProperty fieldAndProperty = new FieldAndProperty() { Field = 7, Property = "p" };

        protected CommandCompilatorTests()
        {
            this.provider = Spec.CreateProvider();
            this.context = Spec.CreateCapturingContext(provider);
        }

        protected TSpec Spec { get; } = new TSpec();

        private CommandCompilator Compilator => provider.CommandCompilator;

        private CommandTextGenerator Generator => provider.CommandTextGenerator;

        #region Simple

        [Fact]
        public void CompileSimple_Text_KeepsTextAndTextCommandType()
        {
            var compiled = Compilator.CompileSimple("select 1");

            compiled.CommandText.Should().Be("select 1");
            compiled.CommandType.Should().Be(CommandType.Text);

            context.CreateSimple(compiled);

            context.LastCommand.CommandText.Should().Be("select 1");
            context.LastCommand.CommandType.Should().Be(CommandType.Text);
            context.LastCommand.Parameters.Count.Should().Be(0);
        }

        [Fact]
        public void CompileProcedureSimple_Name_KeepsNameAndStoredProcedureCommandType()
        {
            var compiled = Compilator.CompileProcedureSimple("TestProc");

            compiled.CommandText.Should().Be("TestProc");
            compiled.CommandType.Should().Be(CommandType.StoredProcedure);

            context.CreateSimple(compiled);

            context.LastCommand.CommandText.Should().Be("TestProc");
            context.LastCommand.CommandType.Should().Be(CommandType.StoredProcedure);
        }

        [Fact]
        public void CompileSimple_Arguments_NamedByPositionAndNullBecomesDBNull()
        {
            context.CreateSimple(Compilator.CompileSimple("select @p0, @p1, @p2"), 1, "a", null);

            ParametersOf(context.LastCommand).Should().Equal(("@p0", 1), ("@p1", "a"), ("@p2", DBNull.Value));
        }

        [Fact]
        public void CompileSimple_DbParameterArgument_AddedAsIsAndUsesUpItsPosition()
        {
            var parameter = Spec.CreateParameter("@Custom", 5);

            context.CreateSimple(Compilator.CompileSimple("select @p0, @Custom, @p2"), 1, parameter, 3);

            ParametersOf(context.LastCommand).Should().Equal(("@p0", 1), ("@Custom", 5), ("@p2", 3));
            context.LastCommand.Parameters[1].Should().BeSameAs(parameter);
        }

        [Fact]
        public void CompileSimple_ReusedWithDifferentArguments_BuildsSeparateCommands()
        {
            var compiled = Compilator.CompileSimple("select @p0");

            context.CreateSimple(compiled, 1);
            context.CreateSimple(compiled, 2);

            context.Commands.Should().HaveCount(2);
            ParametersOf(context.Commands[0]).Should().Equal(("@p0", 1));
            ParametersOf(context.Commands[1]).Should().Equal(("@p0", 2));
        }

        #endregion

        #region Mapped

        [Theory]
        [InlineData("Text")]
        [InlineData("TextAndNames")]
        [InlineData("TextAndSettings")]
        [InlineData("TextNamesAndSettings")]
        [InlineData("ProtoAndText")]
        [InlineData("ProtoTextAndNames")]
        public void CompileMapped_EachOverload_MapsParametersOnMembers(string overload)
        {
            var command = CompileMappedAndCreate(overload, false);

            command.CommandText.Should().Be(MappedText);
            command.CommandType.Should().Be(CommandType.Text);
            ParametersOf(command).Should().Equal(("@ID", 1), ("@Name", "John"));
        }

        [Theory]
        [InlineData("Text")]
        [InlineData("TextAndNames")]
        [InlineData("TextAndSettings")]
        [InlineData("TextNamesAndSettings")]
        [InlineData("ProtoAndText")]
        [InlineData("ProtoTextAndNames")]
        public void CompileMapped_EachOverloadCaseSensitive_DifferentCaseIsNotMapped(string overload)
        {
            Action act = () => CompileMappedAndCreate(overload, true);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage).WithParameterName("parameters");
        }

        [Fact]
        public void CompileMapped_CaseInsensitiveByDefault_ParametersKeepNamesFromText()
        {
            var compiled = Compilator.CompileMapped<Person>("select @id, @NAME");

            ParametersOf(Create(compiled, person)).Should().Equal(("@id", 1), ("@NAME", "John"));
        }

        [Fact]
        public void CompileMapped_CaseSensitiveWithMatchingCase_MapsParameters()
        {
            var compiled = Compilator.CompileMapped<Person>(MappedText, caseSensitiveParamsMatching: true);

            ParametersOf(Create(compiled, person)).Should().Equal(("@ID", 1), ("@Name", "John"));
        }

        [Fact]
        public void CompileMapped_MappedCommand_KeepsTextAndTextCommandType()
        {
            var compiled = Compilator.CompileMapped<Person>(MappedText);

            compiled.CommandText.Should().Be(MappedText);
            compiled.CommandType.Should().Be(CommandType.Text);
        }

        [Fact]
        public void CompileMapped_ExplicitNames_UsedInsteadOfNamesInText()
        {
            var compiled = Compilator.CompileMapped<Person>(MappedText, new[] { "Name" });

            ParametersOf(Create(compiled, person)).Should().Equal(("Name", "John"));
        }

        [Fact]
        public void CompileMapped_DefaultFromTypeOption_UsesFieldsAndProperties()
        {
            var compiled = Compilator.CompileMapped<FieldAndProperty>("select @Field, @Property");

            ParametersOf(Create(compiled, fieldAndProperty)).Should().Equal(("@Field", 7), ("@Property", "p"));
        }

        [Fact]
        public void CompileMapped_FromTypeOptionOverloads_UseOnlySelectedMembers()
        {
            var names = new[] { "@Field" };

            var compiled = new[]
            {
                Compilator.CompileMapped<FieldAndProperty>("select @Field", FromTypeOption.PublicField),
                Compilator.CompileMapped<FieldAndProperty>("select @Field", names, FromTypeOption.PublicField),
                Compilator.CompileMapped(fieldAndProperty, "select @Field", FromTypeOption.PublicField),
                Compilator.CompileMapped(fieldAndProperty, "select @Field", names, FromTypeOption.PublicField)
            };

            foreach (var item in compiled)
            {
                ParametersOf(Create(item, fieldAndProperty)).Should().Equal(("@Field", 7));
            }

            var withPropertiesOnly = new Action[]
            {
                () => Compilator.CompileMapped<FieldAndProperty>("select @Field", FromTypeOption.PublicProperty),
                () => Compilator.CompileMapped<FieldAndProperty>("select @Field", names, FromTypeOption.PublicProperty),
                () => Compilator.CompileMapped(fieldAndProperty, "select @Field", FromTypeOption.PublicProperty),
                () => Compilator.CompileMapped(fieldAndProperty, "select @Field", names, FromTypeOption.PublicProperty)
            };

            foreach (var act in withPropertiesOnly)
            {
                act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
            }
        }

        [Fact]
        public void CompileMapped_Settings_ValuesComeFromSettingGetters()
        {
            var settings = new[]
            {
                new FieldSettings<Person>("Code", typeof(int), x => x.ID * 10),
                new FieldSettings<Person>("Title", typeof(string), x => x.Name.ToUpperInvariant())
            };

            ParametersOf(Create(Compilator.CompileMapped("select @Code, @Title", settings), person))
                .Should().Equal(("@Code", 10), ("@Title", "JOHN"));
            ParametersOf(Create(Compilator.CompileMapped("select @Code, @Title", new[] { "Title" }, settings), person))
                .Should().Equal(("Title", "JOHN"));
        }

        [Fact]
        public void CompileMapped_ParameterWithoutSetting_ThrowsArgumentException()
        {
            Action act = () => Compilator.CompileMapped<Person>("select @ID, @Unknown");

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage).WithParameterName("parameters");
        }

        [Theory]
        [InlineData(false, "ID", "@ID")]
        [InlineData(false, "ID", "id")]
        [InlineData(true, "ID", "@ID")]
        [InlineData(true, "ID", "ID")]
        public void CompileMapped_DuplicateParameterNames_ThrowsArgumentException(bool caseSensitive, string first, string second)
        {
            Action act = () => Compilator.CompileMapped<Person>("select 1", new[] { first, second }, caseSensitiveParamsMatching: caseSensitive);

            act.Should().Throw<ArgumentException>().WithMessage("Parameter names has been duplicated");
        }

        [Fact]
        [Trait("Quirk", "Q33")]
        public void CompileMapped_SameNameInDifferentCaseInText_ThrowsArgumentException()
        {
            Action act = () => Compilator.CompileMapped<Person>("select @ID, @id");

            act.Should().Throw<ArgumentException>().WithMessage("Parameter names has been duplicated");
        }

        [Theory]
        [InlineData(false, "ID", "ID")]
        [InlineData(false, "ID", "id")]
        [InlineData(true, "ID", "ID")]
        public void CompileMapped_DuplicateSettings_ThrowsArgumentException(bool caseSensitive, string first, string second)
        {
            var settings = new[]
            {
                new FieldSettings<Person>(first, typeof(int), x => x.ID),
                new FieldSettings<Person>(second, typeof(int), x => x.ID)
            };

            Action act = () => Compilator.CompileMapped("select @ID", settings, caseSensitive);

            act.Should().Throw<ArgumentException>().WithMessage("Setting names has been duplicated");
        }

        [Fact]
        public void CompileMapped_CaseSensitiveSettingsDifferingByCase_AreNotDuplicates()
        {
            var settings = new[]
            {
                new FieldSettings<Person>("ID", typeof(int), x => x.ID),
                new FieldSettings<Person>("id", typeof(int), x => x.ID * 10)
            };

            var compiled = Compilator.CompileMapped("select @id", settings, true);

            ParametersOf(Create(compiled, person)).Should().Equal(("@id", 10));
        }

        [Theory]
        [Trait("Quirk", "Q17")]
        [InlineData(false)]
        [InlineData(true)]
        public void CompileMapped_NullParameterList_ThrowsArgumentNullException(bool caseSensitive)
        {
            Action act = () => Compilator.CompileMapped<Person>(MappedText, (IList<string>)null, caseSensitiveParamsMatching: caseSensitive);

            act.Should().Throw<ArgumentNullException>().WithParameterName("source");
        }

        [Fact]
        public void CompileMapped_ReusedWithDifferentValues_BuildsSeparateCommands()
        {
            var compiled = Compilator.CompileMapped<Person>("select @ID");

            Create(compiled, new Person() { ID = 1 });
            Create(compiled, new Person() { ID = 2 });

            context.Commands.Should().HaveCount(2);
            ParametersOf(context.Commands[0]).Should().Equal(("@ID", 1));
            ParametersOf(context.Commands[1]).Should().Equal(("@ID", 2));
        }

        #endregion

        #region Stored procedures

        [Theory]
        [InlineData("Name")]
        [InlineData("NameAndSettings")]
        [InlineData("ProtoAndName")]
        public void CompileProcedure_WithoutNames_ParametersFromAllSettings(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            DbCommand command;

            switch (overload)
            {
                case "Name":
                    command = Create(Compilator.CompileProcedure<Person>("TestProc"), person);
                    break;
                case "NameAndSettings":
                    command = Create(Compilator.CompileProcedure("TestProc", FieldSettings.FromType<Person>()), person);
                    break;
                case "ProtoAndName":
                    command = Create(Compilator.CompileProcedure(proto, "TestProc"), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be("TestProc");
            command.CommandType.Should().Be(CommandType.StoredProcedure);
            ParametersOf(command).Should().Equal(("@ID", 1), ("@Name", "John"), ("@Age", 30));
        }

        [Theory]
        [InlineData("NameAndNames")]
        [InlineData("NameNamesAndSettings")]
        [InlineData("ProtoNameAndNames")]
        public void CompileProcedure_ExplicitNames_OnlyThoseParameters(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            var names = new[] { "@ID", "@Age" };
            DbCommand command;

            switch (overload)
            {
                case "NameAndNames":
                    command = Create(Compilator.CompileProcedure<Person>("TestProc", names), person);
                    break;
                case "NameNamesAndSettings":
                    command = Create(Compilator.CompileProcedure("TestProc", names, FieldSettings.FromType<Person>()), person);
                    break;
                case "ProtoNameAndNames":
                    command = Create(Compilator.CompileProcedure(proto, "TestProc", names), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be("TestProc");
            command.CommandType.Should().Be(CommandType.StoredProcedure);
            ParametersOf(command).Should().Equal(("@ID", 1), ("@Age", 30));
        }

        [Fact]
        public void CompileProcedure_Settings_ParameterNamesFromSettings()
        {
            var settings = new[] { new FieldSettings<Person>("Code", typeof(int), x => x.ID * 10) };

            var compiled = Compilator.CompileProcedure("TestProc", settings);

            compiled.CommandType.Should().Be(CommandType.StoredProcedure);
            ParametersOf(Create(compiled, person)).Should().Equal(("@Code", 10));
        }

        [Fact]
        public void CompileProcedure_FromTypeOptionOverloads_UseOnlySelectedMembers()
        {
            ParametersOf(Create(Compilator.CompileProcedure<FieldAndProperty>("P", FromTypeOption.PublicField), fieldAndProperty))
                .Should().Equal(("@Field", 7));
            ParametersOf(Create(Compilator.CompileProcedure(fieldAndProperty, "P", FromTypeOption.PublicProperty), fieldAndProperty))
                .Should().Equal(("@Property", "p"));
            ParametersOf(Create(Compilator.CompileProcedure<FieldAndProperty>("P", new[] { "@Field" }, FromTypeOption.PublicField), fieldAndProperty))
                .Should().Equal(("@Field", 7));

            Action act = () => Compilator.CompileProcedure(fieldAndProperty, "P", new[] { "@Field" }, FromTypeOption.PublicProperty);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        [Fact]
        public void CompileProcedure_CaseSensitivity_FollowsArgument()
        {
            ParametersOf(Create(Compilator.CompileProcedure<Person>("TestProc", new[] { "@id" }), person)).Should().Equal(("@id", 1));

            Action act = () => Compilator.CompileProcedure<Person>("TestProc", new[] { "@id" }, caseSensitiveParamsMatching: true);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        #endregion

        #region Insert

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileInsert_EachOverload_InsertsMembersExceptIgnored(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            DbCommand command;

            switch (overload)
            {
                case "Settings":
                    command = Create(Compilator.CompileInsert("Person", FieldSettings.FromType<Person>(), "ID"), person);
                    break;
                case "FromTypeOption":
                    command = Create(Compilator.CompileInsert<Person>("Person", FromTypeOption.PublicProperty, "ID"), person);
                    break;
                case "Type":
                    command = Create(Compilator.CompileInsert<Person>("Person", "ID"), person);
                    break;
                case "ProtoFromTypeOption":
                    command = Create(Compilator.CompileInsert(proto, "Person", FromTypeOption.PublicProperty, "ID"), proto);
                    break;
                case "Proto":
                    command = Create(Compilator.CompileInsert(proto, "Person", "ID"), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
            command.CommandType.Should().Be(CommandType.Text);
            ParametersOf(command).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileInsert_FromTypeOption_InsertsSelectedMembersOnly()
        {
            var compiled = Compilator.CompileInsert<FieldAndProperty>("T", FromTypeOption.PublicField);

            compiled.CommandText.Should().Be(Generator.Insert("T", "Field"));
            ParametersOf(Create(compiled, fieldAndProperty)).Should().Equal(("Field", 7));
        }

        [Fact]
        public void CompileInsert_UnknownIgnoreField_IsIgnored()
        {
            var compiled = Compilator.CompileInsert<Person>("Person", "Unknown");

            compiled.CommandText.Should().Be(Generator.Insert("Person", "ID", "Name", "Age"));
        }

        [Fact]
        public void CompileInsert_NullIgnoreList_ThrowsArgumentNullException()
        {
            Action act = () => Compilator.CompileInsert<Person>("Person", (string[])null);

            act.Should().Throw<ArgumentNullException>().WithParameterName("source");
        }

        [Fact]
        public void CompileInsertWithOutput_OutputField_InsertsMembersExceptIgnored()
        {
            var compiled = Compilator.CompileInsertWithOutput("Person", FieldSettings.FromType<Person>(), new[] { "ID" }, "ID");

            compiled.CommandText.Should().Be(Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID"));
            compiled.CommandType.Should().Be(CommandType.Text);
            ParametersOf(Create(compiled, person)).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileInsertWithOutput_SeveralOutputFields_AllInText()
        {
            var compiled = Compilator.CompileInsertWithOutput("Person", FieldSettings.FromType<Person>(), new[] { "ID" }, "ID", "Name");

            compiled.CommandText.Should().Be(Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID", "Name"));
        }

        [Fact]
        public void CompileInsertWithOutput_NoOutputFields_PlainInsert()
        {
            var compiled = Compilator.CompileInsertWithOutput("Person", FieldSettings.FromType<Person>(), new[] { "ID" });

            compiled.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
            ParametersOf(Create(compiled, person)).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileInsertWithOutput_NullIgnoreList_InsertsAllMembers()
        {
            var compiled = Compilator.CompileInsertWithOutput("Person", FieldSettings.FromType<Person>(), null, "ID");

            compiled.CommandText.Should().Be(Generator.InsertWithOutput("Person", new[] { "ID", "Name", "Age" }, "ID"));
            ParametersOf(Create(compiled, person)).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        #endregion

        #region Update

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileUpdate_KeysAndIgnoreFields_KeysInWhereOthersInSet(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            var keys = new List<string>() { "ID" };
            var ignore = new List<string>() { "Age" };
            DbCommand command;

            switch (overload)
            {
                case "Settings":
                    command = Create(Compilator.CompileUpdate("Person", FieldSettings.FromType<Person>(), keys, ignore), person);
                    break;
                case "FromTypeOption":
                    command = Create(Compilator.CompileUpdate<Person>("Person", FromTypeOption.PublicProperty, keys, ignore), person);
                    break;
                case "Type":
                    command = Create(Compilator.CompileUpdate<Person>("Person", keys, ignore), person);
                    break;
                case "ProtoFromTypeOption":
                    command = Create(Compilator.CompileUpdate(proto, "Person", FromTypeOption.PublicProperty, keys, ignore), proto);
                    break;
                case "Proto":
                    command = Create(Compilator.CompileUpdate(proto, "Person", keys, ignore), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name"));
            command.CommandType.Should().Be(CommandType.Text);
            ParametersOf(command).Should().Equal(("ID", 1), ("Name", "John"));
        }

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileUpdate_KeyParams_KeysInWhereAllOthersInSet(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            DbCommand command;

            switch (overload)
            {
                case "Settings":
                    command = Create(Compilator.CompileUpdate("Person", FieldSettings.FromType<Person>(), "ID"), person);
                    break;
                case "FromTypeOption":
                    command = Create(Compilator.CompileUpdate<Person>("Person", FromTypeOption.PublicProperty, "ID"), person);
                    break;
                case "Type":
                    command = Create(Compilator.CompileUpdate<Person>("Person", "ID"), person);
                    break;
                case "ProtoFromTypeOption":
                    command = Create(Compilator.CompileUpdate(proto, "Person", FromTypeOption.PublicProperty, "ID"), proto);
                    break;
                case "Proto":
                    command = Create(Compilator.CompileUpdate(proto, "Person", "ID"), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name", "Age"));
            ParametersOf(command).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileUpdate_CompositeKey_AllKeysInWhere()
        {
            var compiled = Compilator.CompileUpdate<Person>("Person", "ID", "Name");

            compiled.CommandText.Should().Be(Generator.Update("Person", new[] { "ID", "Name" }, "Age"));
            ParametersOf(Create(compiled, person)).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileUpdate_FromTypeOption_UsesSelectedMembersOnly()
        {
            var withField = Compilator.CompileUpdate<FieldAndProperty>("T", FromTypeOption.Default, "Field");
            var withoutField = Compilator.CompileUpdate<FieldAndProperty>("T", FromTypeOption.PublicProperty, "Field");

            withField.CommandText.Should().Be(Generator.Update("T", new[] { "Field" }, "Property"));
            withoutField.CommandText.Should().Be(withField.CommandText);
            ParametersOf(Create(withField, fieldAndProperty)).Should().Equal(("Field", 7), ("Property", "p"));
            ParametersOf(Create(withoutField, fieldAndProperty)).Should().Equal(("Property", "p"));
        }

        [Fact]
        public void CompileUpdate_KeyMissingFromSettings_LeavesItsParameterUnmapped()
        {
            var compiled = Compilator.CompileUpdate<Person>("Person", "Missing");

            compiled.CommandText.Should().Be(Generator.Update("Person", new[] { "Missing" }, "ID", "Name", "Age"));
            ParametersOf(Create(compiled, person)).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        #endregion

        #region Delete

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileDelete_EachOverloadWithKeys_KeysInWhere(string overload)
        {
            var command = CompileDeleteAndCreate(overload, "ID");

            command.CommandText.Should().Be(Generator.Delete("Person", new[] { "ID" }));
            command.CommandType.Should().Be(CommandType.Text);
            ParametersOf(command).Should().Equal(("ID", 1));
        }

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileDelete_EachOverloadWithoutKeys_AllMembersAreKeys(string overload)
        {
            var command = CompileDeleteAndCreate(overload);

            command.CommandText.Should().Be(Generator.Delete("Person", new[] { "ID", "Name", "Age" }));
            ParametersOf(command).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileDelete_NullKeys_AllMembersAreKeys()
        {
            var compiled = Compilator.CompileDelete<Person>("Person", (string[])null);

            compiled.CommandText.Should().Be(Generator.Delete("Person", new[] { "ID", "Name", "Age" }));
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("id")]
        public void CompileDelete_KeyNotMatchingSettingsExactly_ThrowsArgumentException(string key)
        {
            Action act = () => Compilator.CompileDelete<Person>("Person", key);

            act.Should().Throw<ArgumentException>().WithMessage(NotMappedMessage);
        }

        #endregion

        #region Merge

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileMerge_KeysAndIgnoreFields_MergesOnKeys(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            var keys = new List<string>() { "ID" };
            var ignore = new List<string>() { "Age" };
            DbCommand command;

            switch (overload)
            {
                case "Settings":
                    command = Create(Compilator.CompileMerge("Person", FieldSettings.FromType<Person>(), keys, ignore), person);
                    break;
                case "FromTypeOption":
                    command = Create(Compilator.CompileMerge<Person>("Person", FromTypeOption.PublicProperty, keys, ignore), person);
                    break;
                case "Type":
                    command = Create(Compilator.CompileMerge<Person>("Person", keys, ignore), person);
                    break;
                case "ProtoFromTypeOption":
                    command = Create(Compilator.CompileMerge(proto, "Person", FromTypeOption.PublicProperty, keys, ignore), proto);
                    break;
                case "Proto":
                    command = Create(Compilator.CompileMerge(proto, "Person", keys, ignore), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be(Generator.Merge("Person", new[] { "ID" }, "Name"));
            command.CommandType.Should().Be(CommandType.Text);
            ParametersOf(command).Should().Equal(("ID", 1), ("Name", "John"));
        }

        [Theory]
        [InlineData("Settings")]
        [InlineData("FromTypeOption")]
        [InlineData("Type")]
        [InlineData("ProtoFromTypeOption")]
        [InlineData("Proto")]
        public void CompileMerge_KeyParams_MergesAllOtherMembers(string overload)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };
            DbCommand command;

            switch (overload)
            {
                case "Settings":
                    command = Create(Compilator.CompileMerge("Person", FieldSettings.FromType<Person>(), "ID"), person);
                    break;
                case "FromTypeOption":
                    command = Create(Compilator.CompileMerge<Person>("Person", FromTypeOption.PublicProperty, "ID"), person);
                    break;
                case "Type":
                    command = Create(Compilator.CompileMerge<Person>("Person", "ID"), person);
                    break;
                case "ProtoFromTypeOption":
                    command = Create(Compilator.CompileMerge(proto, "Person", FromTypeOption.PublicProperty, "ID"), proto);
                    break;
                case "Proto":
                    command = Create(Compilator.CompileMerge(proto, "Person", "ID"), proto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }

            command.CommandText.Should().Be(Generator.Merge("Person", new[] { "ID" }, "Name", "Age"));
            ParametersOf(command).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CompileMerge_FromTypeOption_UsesSelectedMembersOnly()
        {
            var withField = Compilator.CompileMerge<FieldAndProperty>("T", FromTypeOption.Default, "Field");
            var withoutField = Compilator.CompileMerge<FieldAndProperty>("T", FromTypeOption.PublicProperty, "Field");

            withField.CommandText.Should().Be(Generator.Merge("T", new[] { "Field" }, "Property"));
            withoutField.CommandText.Should().Be(withField.CommandText);
            ParametersOf(Create(withField, fieldAndProperty)).Should().Equal(("Field", 7), ("Property", "p"));
            ParametersOf(Create(withoutField, fieldAndProperty)).Should().Equal(("Property", "p"));
        }

        [Fact]
        public void CompileMerge_KeyMissingFromSettings_LeavesItsParameterUnmapped()
        {
            var compiled = Compilator.CompileMerge<Person>("Person", "Missing");

            compiled.CommandText.Should().Be(Generator.Merge("Person", new[] { "Missing" }, "ID", "Name", "Age"));
            ParametersOf(Create(compiled, person)).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        #endregion

        #region Providers

        [Fact]
        public void CreateSimple_CommandCompiledByAnotherProviderInstance_ThrowsArgumentException()
        {
            var compiled = Spec.CreateProvider().CommandCompilator.CompileSimple("select 1");

            Action act = () => context.CreateSimple(compiled);

            act.Should().Throw<ArgumentException>()
                .WithMessage("ContextProvider presented in received ScopedContext does not match stored in given SimpleCommand (Parameter 'scopedContext')")
                .WithParameterName("scopedContext");
            context.Commands.Should().BeEmpty();
        }

        [Fact]
        public void CreateMapped_CommandCompiledByAnotherProviderInstance_ThrowsArgumentException()
        {
            var compiled = Spec.CreateProvider().CommandCompilator.CompileMapped<Person>("select @ID");

            Action act = () => context.CreateMapped(compiled, person);

            act.Should().Throw<ArgumentException>()
                .WithMessage("ContextProvider presented in received ScopedContext does not match stored in given MappedCommand (Parameter 'scopedContext')")
                .WithParameterName("scopedContext");
            context.Commands.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_Provider_CompilesCommandsForThatProvider()
        {
            var compilator = new CommandCompilator(provider);

            compilator.Should().NotBeSameAs(provider.CommandCompilator);

            context.CreateSimple(compilator.CompileSimple("select @p0"), 1);
            ParametersOf(context.LastCommand).Should().Equal(("@p0", 1));

            var compiled = compilator.CompileInsert<Person>("Person", "ID");
            compiled.CommandText.Should().Be(provider.CommandCompilator.CompileInsert<Person>("Person", "ID").CommandText);
            ParametersOf(Create(compiled, person)).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void Constructor_AnotherProvider_CommandsDoNotFitContext()
        {
            var compiled = new CommandCompilator(Spec.CreateProvider()).CompileSimple("select 1");

            Action act = () => context.CreateSimple(compiled);

            act.Should().Throw<ArgumentException>().WithParameterName("scopedContext");
        }

        [Fact]
        public void CommandCompilator_OfContext_IsProviderCompilator()
        {
            context.Context.CommandCompilator.Should().BeSameAs(provider.CommandCompilator);
        }

        #endregion

        #region Parameter values

        [Fact]
        public void CompileMapped_NullMember_BecomesDBNull()
        {
            var compiled = Compilator.CompileMapped<Person>("select @Name, @Age");

            ParametersOf(Create(compiled, new Person() { ID = 1 })).Should().Equal(("@Name", DBNull.Value), ("@Age", DBNull.Value));
        }

        [Fact]
        public void CompileMapped_CustomIsNullGetter_DecidesNullWithoutCallingGetter()
        {
            var settings = new[]
            {
                new FieldSettings<Person>("ID", typeof(int), x => x.ID, x => x.ID == 0),
                new FieldSettings<Person>("Age", typeof(int?), x => throw new InvalidOperationException("Getter must not be called"), x => true)
            };
            var compiled = Compilator.CompileMapped("select @ID, @Age", settings);

            ParametersOf(Create(compiled, new Person() { ID = 0, Age = 5 })).Should().Equal(("@ID", DBNull.Value), ("@Age", DBNull.Value));
            ParametersOf(Create(compiled, new Person() { ID = 3, Age = 5 })).Should().Equal(("@ID", 3), ("@Age", DBNull.Value));
        }

        [Fact]
        public void CompileMapped_DbParameterMember_AddedAsIsWithItsOwnName()
        {
            var parameter = Spec.CreateParameter("@Out", 5, ParameterDirection.Output);
            var compiled = Compilator.CompileMapped<WithParameter>("select @ID, @Result");

            var command = Create(compiled, new WithParameter() { ID = 1, Result = parameter });

            ParametersOf(command).Should().Equal(("@ID", 1), ("@Out", 5));
            command.Parameters[1].Should().BeSameAs(parameter);
        }

        #endregion

        private DbCommand CompileMappedAndCreate(string overload, bool caseSensitive)
        {
            // with case-sensitive matching the lower-case names in the text or the list do not match the members
            var text = caseSensitive ? MappedText.Replace("@ID", "@id") : MappedText;
            var names = caseSensitive ? new[] { "@id", "@Name" } : new[] { "@ID", "@Name" };
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };

            switch (overload)
            {
                case "Text":
                    return Create(Compilator.CompileMapped<Person>(text, caseSensitiveParamsMatching: caseSensitive), person);
                case "TextAndNames":
                    return Create(Compilator.CompileMapped<Person>(MappedText, names, caseSensitiveParamsMatching: caseSensitive), person);
                case "TextAndSettings":
                    return Create(Compilator.CompileMapped(text, FieldSettings.FromType<Person>(), caseSensitive), person);
                case "TextNamesAndSettings":
                    return Create(Compilator.CompileMapped(MappedText, names, FieldSettings.FromType<Person>(), caseSensitive), person);
                case "ProtoAndText":
                    return Create(Compilator.CompileMapped(proto, text, caseSensitiveParamsMatching: caseSensitive), proto);
                case "ProtoTextAndNames":
                    return Create(Compilator.CompileMapped(proto, MappedText, names, caseSensitiveParamsMatching: caseSensitive), proto);
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }
        }

        private DbCommand CompileDeleteAndCreate(string overload, params string[] keys)
        {
            var proto = new { ID = 1, Name = "John", Age = (int?)30 };

            switch (overload)
            {
                case "Settings":
                    return Create(Compilator.CompileDelete("Person", FieldSettings.FromType<Person>(), keys), person);
                case "FromTypeOption":
                    return Create(Compilator.CompileDelete<Person>("Person", FromTypeOption.PublicProperty, keys), person);
                case "Type":
                    return Create(Compilator.CompileDelete<Person>("Person", keys), person);
                case "ProtoFromTypeOption":
                    return Create(Compilator.CompileDelete(proto, "Person", FromTypeOption.PublicProperty, keys), proto);
                case "Proto":
                    return Create(Compilator.CompileDelete(proto, "Person", keys), proto);
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload), overload, "Unknown overload");
            }
        }

        private DbCommand Create<T>(MappedCommand<T> compiled, T value)
        {
            context.CreateMapped(compiled, value);
            return context.LastCommand;
        }

        private static List<(string Name, object Value)> ParametersOf(DbCommand command)
        {
            return command.Parameters.Cast<DbParameter>().Select(x => (x.ParameterName, x.Value)).ToList();
        }
    }
}
