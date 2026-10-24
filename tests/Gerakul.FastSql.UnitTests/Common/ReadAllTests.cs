using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // DataReaderExtensions.ReadAll<T> directly on readers. The async twins are in ReadAllAsyncTests,
    // the FieldsSelector matrix in FieldsSelectorTests, value conversions in ValueConversionTests.
    public class ReadAllTests
    {
        private const string DestinationMessage = "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";
        private const string SourceMessage = "FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination";

        public class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        public class FieldsAndProperties
        {
            public int ID;
            public string Name;
            public string Phone { get; set; }
            public int? Age { get; set; }
        }

        public class WithArray
        {
            public int ID { get; set; }
            public int[] Values { get; set; }
        }

        public class WithEnumerable
        {
            public int ID { get; set; }
            public IEnumerable<string> Tags { get; set; }
        }

        public class WithBinary
        {
            public int ID { get; set; }
            public byte[] Data { get; set; }
            public string Name { get; set; }
        }

        public class CaseConflict
        {
            public int value;
            public int Value { get; set; }
        }

        public class HiddenFieldBase
        {
            public int Value;
        }

        public class PropertyHidingField : HiddenFieldBase
        {
            public new int Value { get; set; }
        }

        public class Nullables
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
            public DateTime? StartWorking { get; set; }
            public byte[] Data { get; set; }
            public string NameField;
            public int? AgeField;
        }

        public class FieldConversions
        {
            public long Number;
            public Color Color;
            public Color? NullableColor;
        }

        public enum Color
        {
            Red = 1,
            Green = 2
        }

        public class WithGetOnly
        {
            public int ID { get; set; }
            public string Computed => "computed";
        }

        public class WithPrivateSetter
        {
            public int ID { get; private set; }
            public string Name { get; set; }
        }

        public class WithHiddenMembers
        {
            public static int Count { get; set; }
            public int ID { get; set; }
            internal string Secret { get; set; }
        }

        public class BaseEntity
        {
            public int ID { get; set; }
            public string Code;
        }

        public class DerivedEntity : BaseEntity
        {
            public string Name { get; set; }
        }

        public class WithIndexer
        {
            private readonly Dictionary<int, string> items = new Dictionary<int, string>();

            public int ID { get; set; }

            public string this[int index]
            {
                get
                {
                    return items[index];
                }
                set
                {
                    items[index] = value;
                }
            }
        }

        private static ReaderTable People()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate");
        }

        #region Members

        [Fact]
        public void ReadAll_PropertiesAndFields_MapsBoth()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).Row(2, "Kate", null, null)
                .CreateReader();

            var result = reader.ReadAll<FieldsAndProperties>().ToList();

            result.Should().BeEquivalentTo(new[]
            {
                new FieldsAndProperties() { ID = 1, Name = "John", Phone = "111", Age = 40 },
                new FieldsAndProperties() { ID = 2, Name = "Kate" }
            }, o => o.WithStrictOrdering());
        }

        [Fact]
        public void ReadAll_WithoutOptions_MapsLikeWithOptions()
        {
            var withoutOptions = People().CreateReader().ReadAll<Person>().ToList();
            var withNullOptions = People().CreateReader().ReadAll<Person>(null).ToList();

            withoutOptions.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" }, new Person() { ID = 2, Name = "Kate" } },
                o => o.WithStrictOrdering());
            withNullOptions.Should().BeEquivalentTo(withoutOptions, o => o.WithStrictOrdering());
        }

        [Fact]
        public void ReadAll_EmptyReader_ReturnsNothing()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").CreateReader();

            reader.ReadAll<Person>().Should().BeEmpty();
        }

        [Fact]
        public void ReadAll_ColumnOrderDiffersFromMemberOrder_MapsByName()
        {
            var reader = new ReaderTable().Column<string>("Name").Column<int>("ID").Row("John", 1).CreateReader();

            var result = reader.ReadAll<Person>().ToList();

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public void ReadAll_InterfaceOnlyReader_MapsRows()
        {
            var reader = People().CreateInterfaceOnlyReader();

            var result = reader.ReadAll<Person>().ToList();

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" }, new Person() { ID = 2, Name = "Kate" } },
                o => o.WithStrictOrdering());
            reader.ReadCalls.Should().Be(3);
        }

        [Fact]
        public void ReadAll_InheritedMembers_AreMapped()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Code").Column<string>("Name").Row(1, "A1", "John").CreateReader();

            var result = reader.ReadAll<DerivedEntity>().ToList();

            result.Should().ContainSingle();
            result[0].ID.Should().Be(1);
            result[0].Code.Should().Be("A1");
            result[0].Name.Should().Be("John");
        }

        [Fact]
        public void ReadAll_StaticAndNonPublicMembers_AreNotDestinationMembers()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int>("Count").Column<string>("Secret").Row(1, 5, "s").CreateReader();
            var sourceReader = new ReaderTable().Column<int>("ID").Column<int>("Count").Column<string>("Secret").Row(1, 5, "s").CreateReader();

            var result = reader.ReadAll<WithHiddenMembers>().ToList();
            Action source = () => sourceReader.ReadAll<WithHiddenMembers>(new ReadOptions(fieldsSelector: FieldsSelector.Source)).ToList();

            result.Should().ContainSingle();
            result[0].ID.Should().Be(1);
            result[0].Secret.Should().BeNull();
            WithHiddenMembers.Count.Should().Be(0);
            source.Should().ThrowExactly<InvalidOperationException>().WithMessage(SourceMessage);
        }

        [Fact]
        public void ReadAll_PrivateSetter_IsSet()
        {
            var reader = People().CreateReader();

            var result = reader.ReadAll<WithPrivateSetter>().ToList();

            result.Select(x => x.ID).Should().Equal(1, 2);
        }

        [Fact]
        [Trait("Quirk", "Q13")]
        public void ReadAll_GetOnlyPropertyWithoutColumn_IsRequiredByDestination()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            Action act = () => reader.ReadAll<WithGetOnly>().ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        [Trait("Quirk", "Q13")]
        public void ReadAll_GetOnlyPropertyWithColumn_ThrowsWhenSet()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Computed").Row(1, "x").CreateReader();

            Action act = () => reader.ReadAll<WithGetOnly>().ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Property set method not found.");
        }

        [Fact]
        [Trait("Quirk", "Q13")]
        public void ReadAll_GetOnlyPropertyWithCommonSelector_IsSkipped()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = reader.ReadAll<WithGetOnly>(new ReadOptions(fieldsSelector: FieldsSelector.Common)).ToList();

            result.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Quirk", "Q53")]
        public void ReadAll_IndexerWithoutColumn_IsRequiredByDestination()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();
            var commonReader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            Action act = () => reader.ReadAll<WithIndexer>().ToList();
            var common = commonReader.ReadAll<WithIndexer>(new ReadOptions(fieldsSelector: FieldsSelector.Common)).ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
            common.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Quirk", "Q53")]
        public void ReadAll_IndexerWithItemColumn_ThrowsWhenSet()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Item").Row(1, "x").CreateReader();

            Action act = () => reader.ReadAll<WithIndexer>().ToList();

            act.Should().ThrowExactly<TargetParameterCountException>().WithMessage("Parameter count mismatch.");
        }

        #endregion

        #region FromTypeOption

        [Fact]
        public void ReadAll_PublicFieldOption_MapsOnlyFields()
        {
            var options = new ReadOptions(fromTypeOption: FromTypeOption.PublicField);
            var allColumns = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).CreateReader();
            var fieldColumns = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").CreateReader();

            var fromAll = allColumns.ReadAll<FieldsAndProperties>(options).ToList();
            var fromFieldColumns = fieldColumns.ReadAll<FieldsAndProperties>(options).ToList();

            fromAll.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { ID = 1, Name = "John" } });
            fromFieldColumns.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { ID = 1, Name = "John" } });
        }

        [Fact]
        public void ReadAll_PublicPropertyOption_MapsOnlyProperties()
        {
            var options = new ReadOptions(fromTypeOption: FromTypeOption.PublicProperty);
            var allColumns = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).CreateReader();
            var propertyColumns = new ReaderTable().Column<string>("Phone").Column<int>("Age").Row("111", 40).CreateReader();

            var fromAll = allColumns.ReadAll<FieldsAndProperties>(options).ToList();
            var fromPropertyColumns = propertyColumns.ReadAll<FieldsAndProperties>(options).ToList();

            fromAll.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { Phone = "111", Age = 40 } });
            fromPropertyColumns.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { Phone = "111", Age = 40 } });
        }

        [Fact]
        public void ReadAll_DefaultOption_SkipsArrayMember()
        {
            var withArrayColumn = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();
            var withoutArrayColumn = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var fromArrayColumn = withArrayColumn.ReadAll<WithArray>().ToList();
            var fromIdOnly = withoutArrayColumn.ReadAll<WithArray>().ToList();

            fromArrayColumn.Should().ContainSingle().Which.Values.Should().BeNull();
            fromIdOnly.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        public void ReadAll_CollectionOption_MapsArrayColumn()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();

            var result = reader.ReadAll<WithArray>(new ReadOptions(fromTypeOption: FromTypeOption.All)).ToList();

            result.Should().ContainSingle();
            result[0].ID.Should().Be(1);
            result[0].Values.Should().Equal(1, 2);
        }

        [Fact]
        public void ReadAll_CollectionOptionAlone_MapsNoMembers()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();

            var result = reader.ReadAll<WithArray>(new ReadOptions(fromTypeOption: FromTypeOption.Collection)).ToList();

            result.Should().ContainSingle();
            result[0].ID.Should().Be(0);
            result[0].Values.Should().BeNull();
        }

        [Fact]
        [Trait("Quirk", "Q52")]
        public void ReadAll_ArrayColumnIntoInterfaceMember_ThrowsInvalidCastException()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string[]>("Tags").Row(1, new[] { "a", "b" }).CreateReader();

            Action act = () => reader.ReadAll<WithEnumerable>(new ReadOptions(fromTypeOption: FromTypeOption.All)).ToList();

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object must implement IConvertible.");
        }

        [Fact]
        public void ReadAll_DefaultOption_MapsByteArrayAndStringMembers()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<byte[]>("Data").Column<string>("Name")
                .Row(1, new byte[] { 1, 2 }, "John").CreateReader();

            var result = reader.ReadAll<WithBinary>().ToList();

            result.Should().ContainSingle();
            result[0].Data.Should().Equal(1, 2);
            result[0].Name.Should().Be("John");
        }

        #endregion

        #region Case sensitivity and duplicates

        [Fact]
        public void ReadAll_ColumnsInOtherCase_AreMatchedByDefault()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            var result = reader.ReadAll<Person>().ToList();

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public void ReadAll_CaseSensitiveColumnsInOtherCase_AreNotMatched()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();
            var commonReader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            Action act = () => reader.ReadAll<Person>(new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();
            var common = commonReader.ReadAll<Person>(new ReadOptions(FieldsSelector.Common, true)).ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
            common.Should().BeEquivalentTo(new[] { new Person() });
        }

        [Fact]
        public void ReadAll_CaseSensitiveColumnsInSameCase_AreMatched()
        {
            var reader = People().CreateReader();

            var result = reader.ReadAll<Person>(new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();

            result.Select(x => x.Name).Should().Equal("John", "Kate");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadAll_DuplicateColumnNames_ThrowsArgumentException(bool caseSensitive)
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Other").Row(1, "John", 2)
                .CreateInterfaceOnlyReader("ID", "Name", "ID");

            Action act = () => reader.ReadAll<Person>(new ReadOptions(caseSensitiveFieldsMatching: caseSensitive)).ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Fact]
        public void ReadAll_ColumnNamesDifferingByCase_ThrowsArgumentException()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("name").Row(1, "John", "john").CreateReader();

            Action act = () => reader.ReadAll<Person>().ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Fact]
        public void ReadAll_CaseSensitiveColumnNamesDifferingByCase_MapsExactMatch()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("name").Row(1, "John", "john").CreateReader();

            var result = reader.ReadAll<Person>(new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public void ReadAll_FieldAndPropertyDifferingByCase_ThrowsArgumentException()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            Action act = () => reader.ReadAll<CaseConflict>().ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Destination object field names has been duplicated");
        }

        [Fact]
        public void ReadAll_CaseSensitiveFieldAndPropertyDifferingByCase_MapsBoth()
        {
            var reader = new ReaderTable().Column<int>("value").Column<int>("Value").Row(1, 2).CreateReader();

            var result = reader.ReadAll<CaseConflict>(new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();

            result.Should().ContainSingle();
            result[0].value.Should().Be(1);
            result[0].Value.Should().Be(2);
        }

        [Fact]
        public void ReadAll_FieldAndPropertyDifferingByCaseWithPropertyOption_MapsProperty()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            var result = reader.ReadAll<CaseConflict>(new ReadOptions(fromTypeOption: FromTypeOption.PublicProperty)).ToList();

            result.Should().ContainSingle();
            result[0].value.Should().Be(0);
            result[0].Value.Should().Be(2);
        }

        // the only way to get a field and a property with exactly the same name: a property hiding an inherited field
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReadAll_PropertyHidingInheritedFieldOfSameName_ThrowsArgumentException(bool caseSensitive)
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            Action act = () => reader.ReadAll<PropertyHidingField>(new ReadOptions(caseSensitiveFieldsMatching: caseSensitive)).ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Destination object field names has been duplicated");
        }

        #endregion

        #region Nulls and field conversions

        [Fact]
        public void ReadAll_DBNullIntoReferenceAndNullableMembers_SetsNull()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age").Column<DateTime>("StartWorking")
                .Column<byte[]>("Data").Column<string>("NameField").Column<int>("AgeField")
                .Row(1, null, null, null, null, null, null)
                .CreateReader();

            var result = reader.ReadAll<Nullables>().ToList();

            result.Should().BeEquivalentTo(new[] { new Nullables() { ID = 1 } });
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAll_DBNullIntoNonNullableProperty_ThrowsInvalidCastException()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(null, "John").CreateReader();

            Action act = () => reader.ReadAll<Person>().ToList();

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAll_DBNullIntoNonNullableField_ThrowsInvalidCastException()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(null, "John", "111", 40).CreateReader();

            Action act = () => reader.ReadAll<FieldsAndProperties>().ToList();

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Fact]
        public void ReadAll_FieldValues_AreConverted()
        {
            var reader = new ReaderTable().Column<int>("Number").Column<string>("Color").Column<int>("NullableColor")
                .Row(42, "Green", 1)
                .Row(43, "Red", null)
                .CreateReader();

            var result = reader.ReadAll<FieldConversions>().ToList();

            result.Should().BeEquivalentTo(new[]
            {
                new FieldConversions() { Number = 42L, Color = Color.Green, NullableColor = Color.Red },
                new FieldConversions() { Number = 43L, Color = Color.Red, NullableColor = null }
            }, o => o.WithStrictOrdering());
        }

        #endregion

        #region Laziness and reader state

        [Fact]
        public void ReadAll_MappingError_IsThrownOnEnumeration()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = reader.ReadAll<Person>();
            IEnumerator<Person> enumerator = null;
            Action getEnumerator = () => enumerator = result.GetEnumerator();
            Action moveNext = () => enumerator.MoveNext();

            getEnumerator.Should().NotThrow();
            moveNext.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        public void ReadAll_ConversionErrorInLaterRow_IsThrownWhenRowIsReached()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(null, "Kate").CreateReader();

            using (var enumerator = reader.ReadAll<Person>().GetEnumerator())
            {
                enumerator.MoveNext().Should().BeTrue();
                enumerator.Current.Name.Should().Be("John");

                Action moveNext = () => enumerator.MoveNext();
                moveNext.Should().ThrowExactly<InvalidCastException>();
            }
        }

        [Fact]
        public void ReadAll_SecondEnumeration_YieldsNothing()
        {
            var result = People().CreateReader().ReadAll<Person>();

            var first = result.ToList();
            var second = result.ToList();

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        public void ReadAll_PartialEnumeration_NextEnumerationContinuesFromReaderPosition()
        {
            var result = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate").Row(3, "Mike")
                .CreateReader().ReadAll<Person>();

            var first = result.First();
            var rest = result.ToList();

            first.ID.Should().Be(1);
            rest.Select(x => x.ID).Should().Equal(2, 3);
        }

        [Fact]
        public void ReadAll_FullEnumeration_LeavesReaderOpen()
        {
            var reader = People().CreateReader();

            reader.ReadAll<Person>().ToList();

            reader.IsClosed.Should().BeFalse();
        }

        [Fact]
        [Trait("Quirk", "Q31")]
        public void ReadAll_PassedOptions_UnsetValuesAreFilledOnEnumeration()
        {
            var options = new ReadOptions(caseSensitiveFieldsMatching: true);

            var result = People().CreateReader().ReadAll<Person>(options);
            var beforeEnumeration = new { options.FieldsSelector, options.FromTypeOption, options.CaseSensitiveFieldsMatching };
            result.ToList();

            beforeEnumeration.FieldsSelector.Should().BeNull();
            beforeEnumeration.FromTypeOption.Should().BeNull();
            options.FieldsSelector.Should().Be(FieldsSelector.Destination);
            options.FromTypeOption.Should().Be(FromTypeOption.Default);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
        }

        #endregion
    }
}
