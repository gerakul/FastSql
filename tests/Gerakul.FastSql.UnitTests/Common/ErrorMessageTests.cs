using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // I8: the messages of FastSql's own exceptions name what does not match: the fields, members and parameters.
    // The other tests match these messages with wildcards; here are the full texts.
    public class ErrorMessageTests
    {
        public class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        public class CaseConflict
        {
            public int value;
            public int Value { get; set; }
        }

        public record Positional(int ID, string Name);

        private static ReaderTable Table(params string[] columns)
        {
            var table = new ReaderTable();
            foreach (var column in columns)
            {
                table.Column<string>(column);
            }

            return table.Row(columns.Select(x => (object)"1").ToArray());
        }

        #region Reading

        [Fact]
        public void FieldsSelector_Destination_NamesTheMembersWithoutAField()
        {
            Action two = () => Table("ID").CreateReader().ReadAll<Person>().Count();
            Action one = () => Table("ID", "Name", "Unused").CreateReader().ReadAll<Person>().Count();

            two.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Destination: the reader has no field for the members 'Name', 'Age' of Person");
            one.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Destination: the reader has no field for the member 'Age' of Person");
        }

        [Fact]
        public void FieldsSelector_Source_NamesTheFieldsWithoutAMember()
        {
            var options = new ReadOptions(FieldsSelector.Source);

            Action two = () => Table("ID", "Extra", "Name", "Other").CreateReader().ReadAll<Person>(options).Count();
            Action one = () => Table("Extra").CreateReader().ReadAll<Person>(options).Count();

            two.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Source: Person has no member for the reader fields 'Extra', 'Other'");
            one.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Source: Person has no member for the reader field 'Extra'");
        }

        [Fact]
        public void FieldsSelector_Both_NamesWhatIsMissingOnEachSide()
        {
            var options = new ReadOptions(FieldsSelector.Both);

            Action both = () => Table("ID", "Extra").CreateReader().ReadAll<Person>(options).Count();
            Action members = () => Table("ID").CreateReader().ReadAll<Person>(options).Count();
            Action fields = () => Table("ID", "Name", "Age", "Extra").CreateReader().ReadAll<Person>(options).Count();

            both.Should().Throw<InvalidOperationException>().WithMessage(
                "FieldsSelector.Both: the reader has no field for the members 'Name', 'Age' of Person; Person has no member for the reader field 'Extra'");
            members.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Both: the reader has no field for the members 'Name', 'Age' of Person");
            fields.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Both: Person has no member for the reader field 'Extra'");
        }

        [Fact]
        public void FieldsSelector_ConstructorParameters_AreCalledConstructorParameters()
        {
            Action record = () => Table("ID").CreateReader().ReadAll<Positional>().Count();
            Action anonymous = () => Table("ID").CreateReader().ReadAllAnonymous(new { ID = "", Missing = "" }).Count();
            Action source = () => Table("ID", "Name", "Extra").CreateReader().ReadAll<Positional>(new ReadOptions(FieldsSelector.Source)).Count();

            record.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Destination: the reader has no field for the constructor parameter 'Name' of Positional");
            anonymous.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Destination: the reader has no field for the constructor parameter 'Missing' of the anonymous type");
            source.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Source: Positional has no constructor parameter for the reader field 'Extra'");
        }

        [Fact]
        public void DuplicateReaderFields_NameTheField()
        {
            var options = new ReadOptions(FieldsSelector.Common);

            Action ignoringCase = () => Table("ID", "x").CreateInterfaceOnlyReader("ID", "id").ReadAll<Person>(options).Count();
            Action caseSensitive = () => Table("ID", "x").CreateInterfaceOnlyReader("ID", "ID").ReadAll<Person>(new ReadOptions(FieldsSelector.Common, true)).Count();

            ignoringCase.Should().Throw<ArgumentException>()
                .WithMessage("The reader has more than one field named 'id' (names are compared case-insensitively)");
            caseSensitive.Should().Throw<ArgumentException>().WithMessage("The reader has more than one field named 'ID'");
        }

        // what "select count(*), max(x) from ..." gives: two columns without a name
        [Fact]
        public void DuplicateReaderFields_WithoutNames_SaysThatTheNamesAreMissing()
        {
            Action act = () => Table("a", "b").CreateInterfaceOnlyReader("", "").ReadAll<Person>(new ReadOptions(FieldsSelector.Common)).Count();

            act.Should().Throw<ArgumentException>()
                .WithMessage("The reader has more than one field without a name: give the columns names (aliases)");
        }

        [Fact]
        public void MembersThatDifferOnlyByCase_NameTheMembers()
        {
            Action members = () => Table("Value").CreateReader().ReadAll<CaseConflict>().Count();
            Action parameters = () => Table("ID").CreateReader().ReadAllAnonymous(new { ID = "", id = "" }).Count();

            members.Should().Throw<ArgumentException>()
                .WithMessage("The members 'value' and 'Value' of CaseConflict differ only by case (names are compared case-insensitively)");
            parameters.Should().Throw<ArgumentException>()
                .WithMessage("The constructor parameters 'ID' and 'id' of the anonymous type differ only by case (names are compared case-insensitively)");
        }

        [Fact]
        public void ValueThatCannotBeConverted_NamesTheFieldTheValueTypeAndTheTargetType()
        {
            var table = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Age");

            Action text = () => table.Row(1, "John", "abc").CreateReader().ReadAll<Person>().Count();
            Action nullValue = () => new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age").Row(null, "John", 30)
                .CreateReader().ReadAll<Person>().Count();

            // the exception of the conversion is kept as the inner exception
            text.Should().ThrowExactly<FormatException>()
                .WithMessage("Cannot read field 'Age' (String) into Int32: The input string 'abc' was not in a correct format.")
                .WithInnerExceptionExactly<FormatException>().WithMessage("The input string 'abc' was not in a correct format.");
            nullValue.Should().ThrowExactly<InvalidCastException>()
                .WithMessage("Cannot read field 'ID' (null) into Int32: Object cannot be cast from DBNull to other types.")
                .WithInnerExceptionExactly<InvalidCastException>();
        }

        #endregion

        #region Commands

        private readonly SqlServerSpec spec = new SqlServerSpec();

        [Fact]
        public void ParametersWithoutAField_AreNamed()
        {
            var compilator = spec.CreateProvider().CommandCompilator;

            Action two = () => compilator.CompileMapped<Person>("select @Agee, @ID, @Nam");
            Action one = () => compilator.CompileMapped(new { ID = 1 }, "select @id, @Name");

            two.Should().Throw<ArgumentException>().WithMessage("No field of Person matches the parameters '@Agee', '@Nam' (Parameter 'parameters')");
            one.Should().Throw<ArgumentException>().WithMessage("No field of the anonymous type matches the parameter '@Name' (Parameter 'parameters')");
        }

        [Fact]
        public void DuplicateParametersAndFields_AreNamed()
        {
            var compilator = spec.CreateProvider().CommandCompilator;
            var settings = new[] { new FieldSettings<Person>("ID", typeof(int), x => x.ID), new FieldSettings<Person>("id", typeof(int), x => x.ID) };

            Action parameters = () => compilator.CompileMapped<Person>("select 1", new[] { "@ID", "@Name", "@id" });
            Action exactParameters = () => compilator.CompileMapped<Person>("select 1", new[] { "@ID", "ID" }, caseSensitiveParamsMatching: true);
            Action fields = () => compilator.CompileMapped("select 1", settings);

            parameters.Should().Throw<ArgumentException>().WithMessage("Parameter 'id' is listed more than once (names are compared case-insensitively)");
            exactParameters.Should().Throw<ArgumentException>().WithMessage("Parameter 'ID' is listed more than once");
            fields.Should().Throw<ArgumentException>().WithMessage("More than one field of Person is named 'id' (names are compared case-insensitively)");
        }

        [Fact]
        public void CommandOfAnotherProvider_SaysSo()
        {
            var command = spec.CreateProvider().CommandCompilator.CompileSimple("select 1");
            var context = spec.CreateCapturingContext();

            Action act = () => context.CreateSimple(command);

            act.Should().Throw<ArgumentException>().WithMessage("The command was compiled by another context provider than the one of this context. "
                + "A command can be used only with contexts of the provider that compiled it (Parameter 'scopedContext')");
        }

        [Fact]
        public void ToDataReader_FieldDefinedTwice_NamesTheField()
        {
            var settings = new[] { new FieldSettings<Person>("ID", typeof(int), x => x.ID), new FieldSettings<Person>("id", typeof(int), x => x.ID) };

            Action ignoringCase = () => new Person[0].ToDataReader(settings, false);
            Action caseSensitive = () => new Person[0].ToDataReader(new[] { settings[0], settings[0] }, true);

            ignoringCase.Should().Throw<ArgumentException>()
                .WithMessage("Field 'id' is defined more than once (names are compared case-insensitively) (Parameter 'settings')");
            caseSensitive.Should().Throw<ArgumentException>().WithMessage("Field 'ID' is defined more than once (Parameter 'settings')");
            new Person[0].ToDataReader(settings, true).FieldCount.Should().Be(2);
        }

        #endregion
    }
}
