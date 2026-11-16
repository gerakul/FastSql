using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // The compilator keeps the last command it compiled for a type, per kind of command (CommandCache), when the
    // members come from the type. A call with the same arguments gets that command; any other call compiles.
    // The model types are used by this class only: the cache is kept per type for the whole process.
    public class CommandCacheTests
    {
        private class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        private class Company
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        private const string Text = "select * from Person where ID = @ID and Name = @Name";

        private readonly SqlServerSpec spec = new SqlServerSpec();
        private readonly ContextProvider provider;
        private readonly ICapturingContext context;
        private readonly Person person = new Person() { ID = 1, Name = "John", Age = 30 };

        public CommandCacheTests()
        {
            this.provider = spec.CreateProvider();
            this.context = spec.CreateCapturingContext(provider);
        }

        private CommandCompilator Compilator => provider.CommandCompilator;

        private CommandTextGenerator Generator => provider.CommandTextGenerator;

        private string[] ParameterNames => context.LastCommand.Parameters.Cast<System.Data.Common.DbParameter>().Select(x => x.ParameterName).ToArray();

        #region The same arguments

        [Fact]
        public void CompileInsert_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileInsert<Person>("Person", "ID");
            var second = Compilator.CompileInsert<Person>("Person", "ID");
            var withOption = Compilator.CompileInsert<Person>("Person", FromTypeOption.Default, "ID");
            var withProto = Compilator.CompileInsert(person, "Person", "ID");

            second.Should().BeSameAs(first);
            withOption.Should().BeSameAs(first);
            withProto.Should().BeSameAs(first);
            first.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
        }

        [Fact]
        public void CompileUpdate_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileUpdate<Person>("Person", "ID");
            var second = Compilator.CompileUpdate<Person>("Person", "ID");
            var withLists = Compilator.CompileUpdate<Person>("Person", new List<string>() { "ID" }, null);

            second.Should().BeSameAs(first);
            withLists.Should().BeSameAs(first);
            first.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name", "Age"));
        }

        [Fact]
        public void CompileDelete_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileDelete<Person>("Person", "ID");
            var second = Compilator.CompileDelete<Person>("Person", "ID");

            second.Should().BeSameAs(first);
            first.CommandText.Should().Be(Generator.Delete("Person", new[] { "ID" }));
        }

        [Fact]
        public void CompileDelete_NoKeyFields_NullAndEmptyAreTheSame()
        {
            var first = Compilator.CompileDelete<Person>("Person");
            var second = Compilator.CompileDelete<Person>("Person", (string[])null);

            second.Should().BeSameAs(first);
            first.CommandText.Should().Be(Generator.Delete("Person", "ID", "Name", "Age"));
        }

        [Fact]
        public void CompileMerge_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileMerge<Person>("Person", "ID");
            var second = Compilator.CompileMerge<Person>("Person", "ID");

            second.Should().BeSameAs(first);
            first.CommandText.Should().Be(Generator.Merge("Person", new[] { "ID" }, "Name", "Age"));
        }

        [Fact]
        public void CompileMapped_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileMapped<Person>(Text);
            var second = Compilator.CompileMapped<Person>(Text);
            var withProto = Compilator.CompileMapped(person, Text);

            second.Should().BeSameAs(first);
            withProto.Should().BeSameAs(first);
        }

        [Fact]
        public void CompileMapped_SameNames_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileMapped<Person>(Text, new[] { "@ID", "@Name" });
            var second = Compilator.CompileMapped<Person>(Text, new List<string>() { "@ID", "@Name" });

            second.Should().BeSameAs(first);
        }

        [Fact]
        public void CompileProcedure_SameArguments_ReturnsTheCommandCompiledBefore()
        {
            var first = Compilator.CompileProcedure<Person>("SavePerson");
            var second = Compilator.CompileProcedure<Person>("SavePerson");
            var named = Compilator.CompileProcedure<Person>("SavePerson", new[] { "@ID" });
            var namedAgain = Compilator.CompileProcedure<Person>("SavePerson", new[] { "@ID" });

            second.Should().BeSameAs(first);
            namedAgain.Should().BeSameAs(named);
            named.Should().NotBeSameAs(first);
        }

        #endregion

        #region Other arguments

        [Fact]
        public void CompileInsert_OtherArguments_CompilesForThem()
        {
            var first = Compilator.CompileInsert<Person>("Person", "ID");
            var otherTable = Compilator.CompileInsert<Person>("People", "ID");
            var otherFields = Compilator.CompileInsert<Person>("People", "ID", "Age");
            var noFields = Compilator.CompileInsert<Person>("People");
            var otherOption = Compilator.CompileInsert<Person>("People", FromTypeOption.PublicField);
            var firstAgain = Compilator.CompileInsert<Person>("Person", "ID");

            first.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
            otherTable.CommandText.Should().Be(Generator.Insert("People", "Name", "Age"));
            otherFields.CommandText.Should().Be(Generator.Insert("People", "Name"));
            noFields.CommandText.Should().Be(Generator.Insert("People", "ID", "Name", "Age"));
            otherOption.CommandText.Should().Be(Generator.Insert("People"));
            firstAgain.CommandText.Should().Be(first.CommandText);
            firstAgain.Should().NotBeSameAs(first, "one command is kept per type and kind");
        }

        [Fact]
        public void CompileInsert_NamesChangedInTheSameArray_CompilesForTheNewNames()
        {
            var ignoreFields = new[] { "ID" };
            var first = Compilator.CompileInsert<Person>("Person", ignoreFields);
            ignoreFields[0] = "Name";
            var second = Compilator.CompileInsert<Person>("Person", ignoreFields);

            first.CommandText.Should().Be(Generator.Insert("Person", "Name", "Age"));
            second.CommandText.Should().Be(Generator.Insert("Person", "ID", "Age"));
        }

        [Fact]
        public void CompileUpdate_OtherKeysOrIgnoredFields_CompilesForThem()
        {
            var first = Compilator.CompileUpdate<Person>("Person", "ID");
            var otherKey = Compilator.CompileUpdate<Person>("Person", "Name");
            var twoKeys = Compilator.CompileUpdate<Person>("Person", "ID", "Name");
            var ignored = Compilator.CompileUpdate<Person>("Person", new[] { "ID" }, new[] { "Age" });

            first.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name", "Age"));
            otherKey.CommandText.Should().Be(Generator.Update("Person", new[] { "Name" }, "ID", "Age"));
            twoKeys.CommandText.Should().Be(Generator.Update("Person", new[] { "ID", "Name" }, "Age"));
            ignored.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name"));
        }

        [Fact]
        public void CompileMapped_NoNamesAndNamesFromTheText_AreDifferentCommands()
        {
            var fromText = Compilator.CompileMapped<Person>(Text);
            var noNames = Compilator.CompileMapped<Person>(Text, (IList<string>)null);
            var fromTextAgain = Compilator.CompileMapped<Person>(Text);

            context.CreateMapped(fromText, person);
            ParameterNames.Should().Equal("@ID", "@Name");
            context.CreateMapped(noNames, person);
            ParameterNames.Should().BeEmpty();
            context.CreateMapped(fromTextAgain, person);
            ParameterNames.Should().Equal("@ID", "@Name");
        }

        [Fact]
        public void CompileMapped_OtherTextOrOptions_CompilesForThem()
        {
            const string lowerCase = "select * from Person where ID = @id";

            var first = Compilator.CompileMapped<Person>(lowerCase);
            var otherText = Compilator.CompileMapped<Person>(Text);
            Action caseSensitive = () => Compilator.CompileMapped<Person>(lowerCase, caseSensitiveParamsMatching: true);
            var fields = Compilator.CompileMapped<Person>("select 1", FromTypeOption.PublicField);
            Action fieldsOnly = () => Compilator.CompileMapped<Person>(lowerCase, FromTypeOption.PublicField);

            first.CommandText.Should().Be(lowerCase);
            otherText.CommandText.Should().Be(Text);
            fields.CommandText.Should().Be("select 1");

            // the command compiled without case sensitivity, or with the properties, is not returned for these
            caseSensitive.Should().Throw<ArgumentException>();
            fieldsOnly.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CompileInsert_OtherType_HasItsOwnCommand()
        {
            var person = Compilator.CompileInsert<Person>("T");
            var company = Compilator.CompileInsert<Company>("T");
            var personAgain = Compilator.CompileInsert<Person>("T");

            person.CommandText.Should().Be(Generator.Insert("T", "ID", "Name", "Age"));
            company.CommandText.Should().Be(Generator.Insert("T", "ID", "Name"));
            personAgain.Should().BeSameAs(person);
        }

        [Fact]
        public void CompileInsert_SettingsOfTheCaller_AreCompiledEveryTime()
        {
            var settings = FieldSettings.FromType<Person>();

            var first = Compilator.CompileInsert("Person", settings, "ID");
            var second = Compilator.CompileInsert("Person", settings, "ID");

            second.Should().NotBeSameAs(first);
            second.CommandText.Should().Be(first.CommandText);
        }

        [Fact]
        public void CompileUpdate_NoKeyFields_ThrowsEveryTime()
        {
            Action act = () => Compilator.CompileUpdate<Person>("Person");

            act.Should().Throw<ArgumentException>().WithMessage("At least one key field is required*");
            act.Should().Throw<ArgumentException>().WithMessage("At least one key field is required*");
        }

        #endregion

        #region Providers

        [Fact]
        public void CompileInsert_OtherProvider_CompilesForThatProvider()
        {
            var otherProvider = spec.CreateProvider();
            var otherContext = spec.CreateCapturingContext(otherProvider);

            var first = Compilator.CompileInsert<Person>("Person");
            var second = otherProvider.CommandCompilator.CompileInsert<Person>("Person");
            Action firstOnOtherContext = () => otherContext.CreateMapped(first, person);

            // a command belongs to the provider that compiled it
            second.Should().NotBeSameAs(first);
            otherContext.CreateMapped(second, person);
            otherContext.LastCommand.CommandText.Should().Be(first.CommandText);
            firstOnOtherContext.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CompileUpdate_PostgreSqlAfterSqlServer_GeneratesItsOwnText()
        {
            var postgreSql = new PostgreSqlSpec().CreateProvider();

            var sqlServerCommand = Compilator.CompileUpdate<Person>("Person", "ID");
            var postgreSqlCommand = postgreSql.CommandCompilator.CompileUpdate<Person>("Person", "ID");
            var sqlServerAgain = Compilator.CompileUpdate<Person>("Person", "ID");

            sqlServerCommand.CommandText.Should().Be(Generator.Update("Person", new[] { "ID" }, "Name", "Age"));
            postgreSqlCommand.CommandText.Should().Be(postgreSql.CommandTextGenerator.Update("Person", new[] { "ID" }, "Name", "Age"));
            postgreSqlCommand.CommandText.Should().NotBe(sqlServerCommand.CommandText);
            sqlServerAgain.CommandText.Should().Be(sqlServerCommand.CommandText);
        }

        #endregion

        #region Commands created from a kept command

        [Fact]
        public void CreateInsert_Twice_EachCommandGetsItsOwnValues()
        {
            context.CreateInsert("Person", new Person() { ID = 1, Name = "John", Age = 30 }, "ID");
            var first = context.LastCommand;
            context.CreateInsert("Person", new Person() { ID = 2, Name = "Jane", Age = null }, "ID");
            var second = context.LastCommand;

            second.Should().NotBeSameAs(first);
            second.CommandText.Should().Be(first.CommandText);
            first.Parameters.Cast<System.Data.Common.DbParameter>().Select(x => x.Value).Should().Equal("John", 30);
            second.Parameters.Cast<System.Data.Common.DbParameter>().Select(x => x.Value).Should().Equal("Jane", DBNull.Value);
        }

        [Fact]
        public void CreateMapped_Twice_UsesTheOptionsOfEachCall()
        {
            context.CreateMapped(Text, person, new QueryOptions(11));
            var first = context.LastCommand;
            context.CreateMapped(Text, person, new QueryOptions(22));
            var second = context.LastCommand;

            first.CommandTimeout.Should().Be(11);
            second.CommandTimeout.Should().Be(22);
        }

        [Fact]
        public void CreateInsertWithOutput_OtherOutputFields_CompilesForThem()
        {
            context.CreateInsertWithOutput("Person", person, new[] { "ID" }, "ID");
            var first = context.LastCommand.CommandText;
            context.CreateInsertWithOutput("Person", person, new[] { "ID" }, "ID");
            var same = context.LastCommand.CommandText;
            context.CreateInsertWithOutput("Person", person, new[] { "ID" }, "ID", "Name");
            var otherOutput = context.LastCommand.CommandText;
            context.CreateInsertWithOutput("Person", person, (IList<string>)null, "ID");
            var nothingIgnored = context.LastCommand.CommandText;

            first.Should().Be(Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID"));
            same.Should().Be(first);
            otherOutput.Should().Be(Generator.InsertWithOutput("Person", new[] { "Name", "Age" }, "ID", "Name"));
            nothingIgnored.Should().Be(Generator.InsertWithOutput("Person", new[] { "ID", "Name", "Age" }, "ID"));
        }

        #endregion

        private class Shared
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        // many threads ask for two commands of one type in turn: everyone gets the command it asked for
        [Fact]
        public void CompileInsert_FromManyThreads_EveryCallGetsItsOwnCommand()
        {
            var expected = new[] { Generator.Insert("First", "Name"), Generator.Insert("Second", "ID", "Name") };
            var wrong = new System.Collections.Concurrent.ConcurrentBag<string>();

            Parallel.For(0, 20000, i =>
            {
                var command = i % 2 == 0 ? Compilator.CompileInsert<Shared>("First", "ID") : Compilator.CompileInsert<Shared>("Second");

                if (command.CommandText != expected[i % 2])
                {
                    wrong.Add(command.CommandText);
                }
            });

            wrong.Should().BeEmpty();
        }
    }
}
