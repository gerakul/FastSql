using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // I18: ReadAll<T> / ExecuteQuery<T> no longer need a parameterless constructor. A type that has one (every struct
    // has) is read as before, by setting its members. A type without one, a positional record for example, is read
    // through its public constructor with the most parameters, by the rules of ReadAllAnonymous.
    public class ConstructorReadingTests
    {
        public enum Color
        {
            Red,
            Green
        }

        public record Person(int ID, string Name, int? Age, Color Color);

        public record struct PersonStruct(int ID, string Name);

        public class TwoConstructors
        {
            public TwoConstructors(int id)
            {
                ID = id;
                Used = "id";
            }

            public TwoConstructors(int id, string name)
            {
                ID = id;
                Name = name;
                Used = "id, name";
            }

            public int ID { get; }
            public string Name { get; }
            public string Used { get; }
        }

        public class WithParameterlessConstructor
        {
            public WithParameterlessConstructor()
            {
            }

            public WithParameterlessConstructor(int id, string name)
            {
                throw new InvalidOperationException("The constructor with parameters must not be used");
            }

            public int ID { get; set; }
            public string Name { get; set; }
        }

        public record WithOtherMember(int ID)
        {
            public string Name { get; set; }
        }

        public class NoPublicConstructor
        {
            private NoPublicConstructor(int id)
            {
            }
        }

        public interface IPerson
        {
            int ID { get; set; }
        }

        private static ReaderTable Persons()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age").Column<string>("Color")
                .Row(1, "John", 30, "Green")
                .Row(2, null, null, "Red");
        }

        private static readonly Person[] expected = { new Person(1, "John", 30, Color.Green), new Person(2, null, null, Color.Red) };

        [Fact]
        public void ReadAll_PositionalRecord_IsReadThroughItsConstructor()
        {
            var items = Persons().CreateReader().ReadAll<Person>().ToArray();

            items.Should().Equal(expected);
        }

        [Fact]
        public async Task ReadAllAsync_PositionalRecord_IsReadThroughItsConstructorAsync()
        {
            var items = await Persons().CreateReader().ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            items.Should().Equal(expected);
        }

        [Fact]
        public void ReadAll_PositionalRecord_ParametersMatchFieldsRegardlessOfCaseAndOrder()
        {
            var table = new ReaderTable().Column<string>("color").Column<int>("AGE").Column<string>("name").Column<int>("id").Row("Green", 30, "John", 1);

            var item = table.CreateReader().ReadAll<Person>().Single();

            item.Should().Be(expected[0]);
        }

        [Fact]
        public void ReadAll_PositionalRecord_FieldsSelectorAppliesToTheParameters()
        {
            var withoutAge = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Color").Column<int>("Extra").Row(1, "John", "Green", 5);

            Action destination = () => withoutAge.CreateReader().ReadAll<Person>().Count();
            Action source = () => withoutAge.CreateReader().ReadAll<Person>(new ReadOptions(FieldsSelector.Source)).Count();
            var common = withoutAge.CreateReader().ReadAll<Person>(new ReadOptions(FieldsSelector.Common)).Single();

            destination.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Destination: the reader has no field for the constructor parameter 'Age' of Person");
            source.Should().Throw<InvalidOperationException>()
                .WithMessage("FieldsSelector.Source: Person has no constructor parameter for the reader field 'Extra'");
            common.Should().Be(new Person(1, "John", null, Color.Green));
        }

        [Fact]
        public void ReadAll_RecordStruct_IsReadByItsMembersAsEveryStruct()
        {
            var items = Persons().CreateReader().ReadAll<PersonStruct>(new ReadOptions(FieldsSelector.Common)).ToArray();

            items.Should().Equal(new PersonStruct(1, "John"), new PersonStruct(2, null));
        }

        [Fact]
        public void ReadAll_SeveralConstructors_UsesTheOneWithTheMostParameters()
        {
            var items = Persons().CreateReader().ReadAll<TwoConstructors>(new ReadOptions(FieldsSelector.Common)).ToArray();

            items.Select(x => (x.ID, x.Name, x.Used)).Should().Equal((1, "John", "id, name"), (2, null, "id, name"));
        }

        // what ReadAllAnonymous has always done: the first public constructor
        [Fact]
        public void ReadAllAnonymous_SeveralConstructors_StillUsesTheFirstOne()
        {
            var items = Persons().CreateReader().ReadAllAnonymous(default(TwoConstructors), new ReadOptions(FieldsSelector.Common)).ToArray();

            items.Select(x => (x.ID, x.Name, x.Used)).Should().Equal((1, null, "id"), (2, null, "id"));
        }

        [Fact]
        public void ReadAll_TypeWithParameterlessConstructor_IsReadByItsMembersAsBefore()
        {
            var items = Persons().CreateReader().ReadAll<WithParameterlessConstructor>(new ReadOptions(FieldsSelector.Common)).ToArray();

            items.Select(x => (x.ID, x.Name)).Should().Equal((1, "John"), (2, null));
        }

        // a type that is read through its constructor gets only what the constructor takes
        [Fact]
        public void ReadAll_RecordWithAnotherSettableMember_SetsOnlyTheConstructorParameters()
        {
            var items = Persons().CreateReader().ReadAll<WithOtherMember>(new ReadOptions(FieldsSelector.Common)).ToArray();

            items.Select(x => (x.ID, x.Name)).Should().Equal((1, null), (2, null));
        }

        [Fact]
        public void ReadAll_TypeWithoutPublicConstructor_ThrowsWhenEnumerated()
        {
            var noPublicConstructor = Persons().CreateReader().ReadAll<NoPublicConstructor>();
            var anInterface = Persons().CreateReader().ReadAll<IPerson>();

            noPublicConstructor.Invoking(x => x.Count()).Should().Throw<InvalidOperationException>()
                .WithMessage("Type *+NoPublicConstructor has no public constructor");
            anInterface.Invoking(x => x.Count()).Should().Throw<InvalidOperationException>()
                .WithMessage("Type *+IPerson has no public constructor");
        }

        [Fact]
        public void ExecuteQuery_OnACommand_ReadsAPositionalRecord()
        {
            // a method with the constraint that the reading methods had until phase 4 still compiles and works
            static List<T> ReadWithConstraint<T>(System.Data.IDataReader reader) where T : new()
            {
                return reader.ReadAll<T>(new ReadOptions(FieldsSelector.Common)).ToList();
            }

            ReadWithConstraint<WithParameterlessConstructor>(Persons().CreateReader()).Should().HaveCount(2);
        }
    }
}
