using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    public class FieldSettingsTests
    {
        private enum Color
        {
            Red = 1,
            Green = 2
        }

        private class Item
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        // properties are declared before fields on purpose: FromType returns the fields first anyway
        private class Mixed
        {
            public int PropA { get; set; }
            public int FieldA = 1;
            public string PropB { get; set; }
            public string FieldB = "b";
        }

        private class ValueTypes
        {
            public int? NullableInt { get; set; }
            public Color Color { get; set; }
            public Color? NullableColor { get; set; }
            public decimal Amount = 0;
        }

        private class GetOnly
        {
            public int ID { get; } = 7;
            public string Computed => "computed";
            public int PrivateSetter { get; private set; } = 8;
        }

        private class Collections
        {
            public List<int> ListField = null;
            public string TextField = null;
            public List<int> IntList { get; set; }
            public int[] IntArray { get; set; }
            public IEnumerable<string> Strings { get; set; }
            public Dictionary<string, int> Dictionary { get; set; }
            public char[] Chars { get; set; }
            public ArrayList ArrayList { get; set; }
            public string Text { get; set; }
            public byte[] Bytes { get; set; }
            public int Number { get; set; }
        }

        // a member counts as a collection when its type implements IEnumerable, unless it implements IEnumerable<byte>
        // or is string; an interface type does not implement itself
        private class CollectionEdgeCases
        {
            public IEnumerable NonGenericEnumerable { get; set; }
            public IEnumerable<byte> ByteSequence { get; set; }
            public List<byte> ByteList { get; set; }
            public object Object { get; set; }
        }

        private class Visibility
        {
            public static int StaticField = 1;
            private int privateField = 2;
            internal int InternalField = 3;
            protected int ProtectedField = 4;

            public static int StaticProperty { get; set; }
            private int PrivateProperty { get; set; }
            internal int InternalProperty { get; set; }
            protected int ProtectedProperty { get; set; }
            public int Public { get; set; }

            public int Sum()
            {
                return privateField + InternalField + ProtectedField + PrivateProperty + InternalProperty + ProtectedProperty;
            }
        }

        private class NonPublicAccessors
        {
            private int written;

            public int PrivateGetter { private get; set; }

            public int WriteOnly
            {
                set
                {
                    written = value;
                }
            }

            public int Written()
            {
                return written + PrivateGetter;
            }
        }

        private class Base
        {
            public int BaseField = 1;
            public int BaseProperty { get; set; }
        }

        private class Derived : Base
        {
            public int DerivedField = 2;
            public int DerivedProperty { get; set; }
        }

        private class SameTypeHidingBase
        {
            public int Value { get; set; }
        }

        private class SameTypeHiding : SameTypeHidingBase
        {
            public new int Value { get; set; }
        }

        private class OtherTypeHidingBase
        {
            public int Field = 1;
            public int Value { get; set; }
        }

        private class OtherTypeHiding : OtherTypeHidingBase
        {
            public new string Field = "a";
            public new string Value { get; set; }
        }

        private class WithIndexer
        {
            public int ID { get; set; }

            public int this[int index]
            {
                get
                {
                    return index;
                }
            }
        }

        private class CaseVariants
        {
            public int id = 1;
            public int ID { get; set; }
        }

        private class ThrowingEnumerable : IEnumerable<Item>
        {
            public IEnumerator<Item> GetEnumerator()
            {
                throw new InvalidOperationException("Enumerated");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private static string[] Describe<T>(IEnumerable<FieldSettings<T>> settings)
        {
            return settings.Select(x => $"{x.Name}:{x.FieldType.Name}:{x.DataTypeName}:{x.DetermineNullByValue}").ToArray();
        }

        #region Constructors

        [Fact]
        public void Constructor_DataTypeNameAndIsNullGetter_StoresAllArguments()
        {
            Func<Item, object> getter = x => x.ID;
            Func<Item, bool> isNullGetter = x => false;

            var settings = new FieldSettings<Item>("ID", "custom", typeof(long), getter, isNullGetter);

            settings.Name.Should().Be("ID");
            settings.DataTypeName.Should().Be("custom");
            settings.FieldType.Should().Be(typeof(long));
            settings.Getter.Should().BeSameAs(getter);
            settings.IsNullGetter.Should().BeSameAs(isNullGetter);
            settings.DetermineNullByValue.Should().BeFalse();
        }

        [Fact]
        public void Constructor_IsNullGetterWithoutDataTypeName_DataTypeNameIsFieldTypeName()
        {
            Func<Item, object> getter = x => x.ID;
            Func<Item, bool> isNullGetter = x => false;

            var settings = new FieldSettings<Item>("ID", typeof(long), getter, isNullGetter);

            settings.Name.Should().Be("ID");
            settings.DataTypeName.Should().Be("Int64");
            settings.FieldType.Should().Be(typeof(long));
            settings.Getter.Should().BeSameAs(getter);
            settings.IsNullGetter.Should().BeSameAs(isNullGetter);
            settings.DetermineNullByValue.Should().BeFalse();
        }

        [Fact]
        public void Constructor_DataTypeNameWithoutIsNullGetter_DeterminesNullByValue()
        {
            Func<Item, object> getter = x => x.Name;

            var settings = new FieldSettings<Item>("Name", "custom", typeof(string), getter);

            settings.Name.Should().Be("Name");
            settings.DataTypeName.Should().Be("custom");
            settings.FieldType.Should().Be(typeof(string));
            settings.Getter.Should().BeSameAs(getter);
            settings.DetermineNullByValue.Should().BeTrue();
            settings.IsNullGetter(new Item() { Name = null }).Should().BeTrue();
            settings.IsNullGetter(new Item() { Name = "x" }).Should().BeFalse();
        }

        [Fact]
        public void Constructor_FieldTypeAndGetterOnly_DataTypeNameIsFieldTypeNameAndNullByValue()
        {
            Func<Item, object> getter = x => x.Name;

            var settings = new FieldSettings<Item>("Name", typeof(string), getter);

            settings.Name.Should().Be("Name");
            settings.DataTypeName.Should().Be("String");
            settings.FieldType.Should().Be(typeof(string));
            settings.Getter.Should().BeSameAs(getter);
            settings.DetermineNullByValue.Should().BeTrue();
            settings.IsNullGetter(new Item() { Name = null }).Should().BeTrue();
            settings.IsNullGetter(new Item() { Name = "x" }).Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithoutIsNullGetter_IsNullGetterInvokesGetterEachTime()
        {
            int calls = 0;
            var settings = new FieldSettings<Item>("Name", typeof(string), x =>
            {
                calls++;
                return x.Name;
            });

            settings.IsNullGetter(new Item());
            settings.IsNullGetter(new Item());

            calls.Should().Be(2);
        }

        [Fact]
        public void Constructor_WithoutIsNullGetter_DBNullValueIsNotNull()
        {
            var settings = new FieldSettings<Item>("Name", typeof(string), x => DBNull.Value);

            settings.IsNullGetter(new Item()).Should().BeFalse();
        }

        [Theory]
        [InlineData(typeof(int), "Int32")]
        [InlineData(typeof(int?), "Nullable`1")]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(byte[]), "Byte[]")]
        [InlineData(typeof(List<int>), "List`1")]
        [InlineData(typeof(DateTimeOffset), "DateTimeOffset")]
        public void Constructor_WithoutDataTypeName_DataTypeNameIsShortTypeName(Type fieldType, string expected)
        {
            new FieldSettings<Item>("X", fieldType, x => null).DataTypeName.Should().Be(expected);
            new FieldSettings<Item>("X", fieldType, x => null, x => true).DataTypeName.Should().Be(expected);
        }

        [Fact]
        public void Constructor_WithoutDataTypeNameAndNullFieldType_ThrowsNullReferenceException()
        {
            Action withGetter = () => new FieldSettings<Item>("X", (Type)null, x => null);
            Action withIsNullGetter = () => new FieldSettings<Item>("X", (Type)null, x => null, x => true);

            withGetter.Should().Throw<NullReferenceException>();
            withIsNullGetter.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void Constructor_WithDataTypeNameAndNullFieldType_StoresNull()
        {
            var settings = new FieldSettings<Item>("X", "custom", null, x => null);

            settings.FieldType.Should().BeNull();
            settings.DataTypeName.Should().Be("custom");
        }

        [Fact]
        public void Constructor_NameOnly_UsesIdentityGetterAndTypeOfT()
        {
            var settings = new FieldSettings<string>("Value");

            settings.Name.Should().Be("Value");
            settings.DataTypeName.Should().Be("String");
            settings.FieldType.Should().Be(typeof(string));
            settings.DetermineNullByValue.Should().BeTrue();
            settings.Getter("abc").Should().Be("abc");
            settings.IsNullGetter(null).Should().BeTrue();
            settings.IsNullGetter("abc").Should().BeFalse();
        }

        [Fact]
        public void Constructor_NameOnlyForValueType_BoxesValueAndIsNeverNull()
        {
            var settings = new FieldSettings<int>("Value");

            settings.DataTypeName.Should().Be("Int32");
            settings.FieldType.Should().Be(typeof(int));
            settings.Getter(5).Should().Be(5);
            settings.IsNullGetter(0).Should().BeFalse();
        }

        [Fact]
        public void Constructor_NameOnlyForNullableValueType_DetectsNull()
        {
            var settings = new FieldSettings<int?>("Value");

            settings.DataTypeName.Should().Be("Nullable`1");
            settings.FieldType.Should().Be(typeof(int?));
            settings.Getter(null).Should().BeNull();
            settings.Getter(3).Should().Be(3);
            settings.IsNullGetter(null).Should().BeTrue();
            settings.IsNullGetter(3).Should().BeFalse();
        }

        [Fact]
        public void Constructor_NameOnly_SequenceOfScalarsBecomesOneColumnReader()
        {
            var reader = new[] { "a", null, "c" }.ToDataReader(new FieldSettings<string>("Value"));

            var values = new List<object>();
            var nulls = new List<bool>();
            while (reader.Read())
            {
                values.Add(reader.GetValue(0));
                nulls.Add(reader.IsDBNull(0));
            }

            reader.FieldCount.Should().Be(1);
            reader.GetName(0).Should().Be("Value");
            values.Should().Equal(new object[] { "a", null, "c" });
            nulls.Should().Equal(false, true, false);
        }

        #endregion

        #region FromType

        [Fact]
        public void FromType_NoOption_ReturnsPublicFieldsFirstThenProperties()
        {
            var settings = FieldSettings.FromType<Mixed>();

            settings.GetNames().Should().Equal("FieldA", "FieldB", "PropA", "PropB");
        }

        [Fact]
        public void FromTypeOption_DefaultAndBothAreFieldsAndProperties()
        {
            FromTypeOption.Both.Should().Be(FromTypeOption.Default);
            FromTypeOption.Default.Should().Be(FromTypeOption.PublicField | FromTypeOption.PublicProperty);
            FromTypeOption.All.Should().Be(FromTypeOption.Default | FromTypeOption.Collection);
        }

        // Both has the same value as Default, so the Default row covers it
        [Theory]
        [InlineData(FromTypeOption.PublicField, new[] { "FieldA", "FieldB" })]
        [InlineData(FromTypeOption.PublicProperty, new[] { "PropA", "PropB" })]
        [InlineData(FromTypeOption.Default, new[] { "FieldA", "FieldB", "PropA", "PropB" })]
        [InlineData(FromTypeOption.All, new[] { "FieldA", "FieldB", "PropA", "PropB" })]
        [InlineData(FromTypeOption.PublicField | FromTypeOption.Collection, new[] { "FieldA", "FieldB" })]
        [InlineData(FromTypeOption.PublicProperty | FromTypeOption.Collection, new[] { "PropA", "PropB" })]
        [InlineData(FromTypeOption.Collection, new string[0])]
        [InlineData((FromTypeOption)0, new string[0])]
        public void FromType_Option_SelectsMemberKinds(FromTypeOption option, string[] expected)
        {
            var settings = FieldSettings.FromType<Mixed>(option);

            settings.GetNames().Should().Equal(expected);
        }

        [Fact]
        public void FromType_FieldAndProperty_SettingsDescribeMembers()
        {
            var settings = FieldSettings.FromType<Mixed>();

            Describe(settings).Should().Equal(
                "FieldA:Int32:Int32:True",
                "FieldB:String:String:True",
                "PropA:Int32:Int32:True",
                "PropB:String:String:True");
        }

        [Fact]
        public void FromType_Getters_ReadCurrentMemberValues()
        {
            var settings = FieldSettings.FromType<Mixed>();
            var item = new Mixed();

            item.FieldA = 5;
            item.FieldB = null;
            item.PropA = 6;
            item.PropB = "x";

            settings.Select(x => x.Getter(item)).Should().Equal(5, null, 6, "x");
            settings.Select(x => x.IsNullGetter(item)).Should().Equal(false, true, false, false);
        }

        [Fact]
        public void FromType_NullableAndEnumMembers_KeepDeclaredTypes()
        {
            var settings = FieldSettings.FromType<ValueTypes>();
            var item = new ValueTypes() { NullableInt = null, Color = Color.Green, NullableColor = Color.Red, Amount = 1.5m };

            Describe(settings).Should().Equal(
                "Amount:Decimal:Decimal:True",
                "NullableInt:Nullable`1:Nullable`1:True",
                "Color:Color:Color:True",
                "NullableColor:Nullable`1:Nullable`1:True");
            settings.Select(x => x.Getter(item)).Should().Equal(1.5m, null, Color.Green, Color.Red);
            settings.Select(x => x.IsNullGetter(item)).Should().Equal(false, true, false, false);
        }

        [Fact]
        public void FromType_GetOnlyProperties_AreIncludedAndReadable()
        {
            var settings = FieldSettings.FromType<GetOnly>();

            settings.GetNames().Should().Equal("ID", "Computed", "PrivateSetter");
            settings.Select(x => x.Getter(new GetOnly())).Should().Equal(7, "computed", 8);
        }

        [Fact]
        public void FromType_CollectionMembers_ExcludedByDefault()
        {
            var settings = FieldSettings.FromType<Collections>();

            settings.GetNames().Should().Equal("TextField", "Text", "Bytes", "Number");
        }

        [Fact]
        public void FromType_CollectionOption_IncludesCollectionMembers()
        {
            var settings = FieldSettings.FromType<Collections>(FromTypeOption.All);

            settings.GetNames().Should().Equal("ListField", "TextField", "IntList", "IntArray", "Strings", "Dictionary", "Chars",
                "ArrayList", "Text", "Bytes", "Number");
        }

        [Fact]
        [Trait("Quirk", "Q45")]
        public void FromType_CollectionDetection_LooksOnlyAtImplementedInterfaces()
        {
            var settings = FieldSettings.FromType<CollectionEdgeCases>();

            // IEnumerable itself and List<byte> count as scalars, IEnumerable<byte> counts as a collection
            settings.GetNames().Should().Equal("NonGenericEnumerable", "ByteList", "Object");
        }

        [Fact]
        public void FromType_StaticAndNonPublicMembers_AreExcluded()
        {
            var settings = FieldSettings.FromType<Visibility>(FromTypeOption.All);

            settings.GetNames().Should().Equal("Public");
        }

        [Fact]
        public void FromType_PropertyWithNonPublicGetter_IsIncludedAndReadable()
        {
            var settings = FieldSettings.FromType<NonPublicAccessors>();
            var item = new NonPublicAccessors() { PrivateGetter = 5 };

            settings.GetNames().Should().Equal("PrivateGetter");
            settings[0].Getter(item).Should().Be(5);
        }

        [Fact]
        [Trait("Fixed", "Q46")]
        public void FromType_WriteOnlyProperty_IsExcluded()
        {
            var settings = FieldSettings.FromType<NonPublicAccessors>();

            settings.GetNames().Should().NotContain("WriteOnly");
        }

        [Fact]
        public void FromType_InheritedMembers_AreIncludedAfterDeclaredOnes()
        {
            var settings = FieldSettings.FromType<Derived>();
            var item = new Derived() { BaseProperty = 3, DerivedProperty = 4 };

            settings.GetNames().Should().Equal("DerivedField", "BaseField", "DerivedProperty", "BaseProperty");
            settings.Select(x => x.Getter(item)).Should().Equal(2, 1, 4, 3);
        }

        [Fact]
        public void FromType_PropertyHiddenWithSameType_AppearsOnce()
        {
            var settings = FieldSettings.FromType<SameTypeHiding>();
            var item = new SameTypeHiding() { Value = 5 };
            ((SameTypeHidingBase)item).Value = 6;

            settings.GetNames().Should().Equal("Value");
            settings[0].Getter(item).Should().Be(5);
        }

        [Fact]
        [Trait("Fixed", "Q48")]
        public void FromType_MemberHiddenWithOtherType_AppearsOnceAsHidingMember()
        {
            var settings = FieldSettings.FromType<OtherTypeHiding>();
            var item = new OtherTypeHiding() { Value = "derived" };
            ((OtherTypeHidingBase)item).Value = 6;

            Describe(settings).Should().Equal(
                "Field:String:String:True",
                "Value:String:String:True");
            settings.Select(x => x.Getter(item)).Should().Equal("a", "derived");
        }

        [Fact]
        [Trait("Fixed", "Q21")]
        public void FromType_Indexer_IsExcluded()
        {
            var settings = FieldSettings.FromType<WithIndexer>();

            Describe(settings).Should().Equal("ID:Int32:Int32:True");
        }

        [Fact]
        public void FromType_MembersDifferingOnlyByCase_AreBothIncluded()
        {
            var settings = FieldSettings.FromType<CaseVariants>();
            var item = new CaseVariants() { ID = 2 };

            settings.GetNames().Should().Equal("id", "ID");
            settings.Select(x => x.Getter(item)).Should().Equal(1, 2);
        }

        [Fact]
        public void FromType_EachCall_ReturnsNewInstances()
        {
            var first = FieldSettings.FromType<Item>();
            var second = FieldSettings.FromType<Item>();

            second.Should().NotBeSameAs(first);
            second[0].Should().NotBeSameAs(first[0]);
        }

        [Fact]
        public void FromType_Proto_ReturnsPropertiesOfAnonymousTypeInDeclarationOrder()
        {
            var proto = new { ID = 0, Name = "", Age = (int?)null };

            var settings = FieldSettings.FromType(proto);

            Describe(settings).Should().Equal("ID:Int32:Int32:True", "Name:String:String:True", "Age:Nullable`1:Nullable`1:True");
            var item = new { ID = 5, Name = "x", Age = (int?)7 };
            settings.Select(x => x.Getter(item)).Should().Equal(5, "x", 7);
        }

        [Fact]
        public void FromType_ProtoWithOption_AppliesOption()
        {
            var proto = new { ID = 0, Name = "" };

            FieldSettings.FromType(proto, FromTypeOption.PublicField).Should().BeEmpty();
            FieldSettings.FromType(proto, FromTypeOption.PublicProperty).GetNames().Should().Equal("ID", "Name");
        }

        [Fact]
        public void FromType_NullProto_UsesStaticType()
        {
            var settings = FieldSettings.FromType((Item)null);

            settings.GetNames().Should().Equal("ID", "Name");
        }

        #endregion

        #region GetFields, GetNames, GetFieldSettings

        [Fact]
        public void GetFields_ReturnsNamesOfFromType()
        {
            FieldSettings.GetFields<Mixed>().Should().Equal("FieldA", "FieldB", "PropA", "PropB");
            FieldSettings.GetFields<Mixed>(FromTypeOption.PublicProperty).Should().Equal("PropA", "PropB");
            FieldSettings.GetFields<Collections>(FromTypeOption.PublicField | FromTypeOption.Collection).Should().Equal("ListField", "TextField");
        }

        [Fact]
        public void GetFields_Proto_ReturnsNamesOfAnonymousType()
        {
            var proto = new { B = 1, A = "a" };

            FieldSettings.GetFields(proto).Should().Equal("B", "A");
            FieldSettings.GetFields(proto, FromTypeOption.PublicField).Should().BeEmpty();
        }

        [Fact]
        public void GetNames_ReturnsNamesInOrderIncludingDuplicates()
        {
            var settings = new List<FieldSettings<Item>>()
            {
                new FieldSettings<Item>("B", typeof(int), x => x.ID),
                new FieldSettings<Item>("A", typeof(string), x => x.Name),
                new FieldSettings<Item>("B", typeof(int), x => x.ID)
            };

            settings.GetNames().Should().Equal("B", "A", "B");
            new FieldSettings<Item>[0].GetNames().Should().BeEmpty();
        }

        [Fact]
        public void GetFieldSettings_ReturnsFromTypeOfElementType()
        {
            var values = new List<Mixed>();

            Describe(values.GetFieldSettings()).Should().Equal(Describe(FieldSettings.FromType<Mixed>()));
            values.GetFieldSettings(FromTypeOption.PublicField).GetNames().Should().Equal("FieldA", "FieldB");
        }

        [Fact]
        public void GetFieldSettings_DoesNotEnumerateValues()
        {
            var settings = new ThrowingEnumerable().GetFieldSettings();

            settings.GetNames().Should().Equal("ID", "Name");
            ((IEnumerable<Item>)null).GetFieldSettings().GetNames().Should().Equal("ID", "Name");
        }

        #endregion
    }
}
