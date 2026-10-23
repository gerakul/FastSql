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
    // ReadAllAnonymous / ReadAllAnonymousAsync: reader columns are matched with the parameters of the first public
    // constructor of the proto's type, and the proto value itself is not used. The FieldsSelector matrix for these
    // methods is in FieldsSelectorTests, value conversions in ValueConversionTests.
    public class ReadAllAnonymousTests
    {
        private const string DestinationMessage = "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";

        public class TwoConstructors
        {
            public TwoConstructors(int id, string name)
            {
                this.ID = id;
                this.Name = name;
                this.UsedConstructor = 1;
            }

            public TwoConstructors(string name)
            {
                this.Name = name;
                this.UsedConstructor = 2;
            }

            public int ID { get; }
            public string Name { get; }
            public int UsedConstructor { get; }
        }

        public class CaseConflictConstructor
        {
            public CaseConflictConstructor(int value, int Value)
            {
                this.Lower = value;
                this.Upper = Value;
            }

            public int Lower { get; }
            public int Upper { get; }
        }

        public class PrivateConstructor
        {
            private PrivateConstructor()
            {
            }

            public static PrivateConstructor Create()
            {
                return new PrivateConstructor();
            }
        }

        public enum Color
        {
            Red = 1,
            Green = 2
        }

        private static ReaderTable People()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate");
        }

        #region Matching

        [Fact]
        public void ReadAllAnonymous_AnonymousType_MatchesConstructorParametersByName()
        {
            var reader = new ReaderTable().Column<string>("Name").Column<int>("ID").Row("John", 1).Row("Kate", 2).CreateReader();

            var result = reader.ReadAllAnonymous(new { ID = 0, Name = "" }).ToList();

            result.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_AnonymousType_MatchesConstructorParametersByNameAsync()
        {
            var reader = new ReaderTable().Column<string>("Name").Column<int>("ID").Row("John", 1).Row("Kate", 2).CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }).ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
        }

        [Fact]
        public void ReadAllAnonymous_WithNullOptions_MapsLikeWithoutOptions()
        {
            var result = People().CreateReader().ReadAllAnonymous(new { ID = 0, Name = "" }, null).ToList();

            result.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_WithNullOptions_MapsLikeWithoutOptionsAsync()
        {
            var result = await People().CreateReader().ReadAllAnonymousAsync(new { ID = 0, Name = "" }, null)
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
        }

        [Fact]
        public void ReadAllAnonymous_EmptyReader_ReturnsNothing()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").CreateReader();

            reader.ReadAllAnonymous(new { ID = 0, Name = "" }).Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_EmptyReader_ReturnsNothingAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }).ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEmpty();
        }

        [Fact]
        public void ReadAllAnonymous_MissingColumnsWithCommon_GetDefaultValuesNotProtoValues()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = reader.ReadAllAnonymous(new { ID = 5, Name = "proto", Age = 7 }, new ReadOptions(fieldsSelector: FieldsSelector.Common)).ToList();

            result.Should().Equal(new { ID = 1, Name = (string)null, Age = 0 });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_MissingColumnsWithCommon_GetDefaultValuesNotProtoValuesAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new { ID = 5, Name = "proto", Age = 7 }, new ReadOptions(fieldsSelector: FieldsSelector.Common))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(new { ID = 1, Name = (string)null, Age = 0 });
        }

        [Fact]
        public void ReadAllAnonymous_MissingColumnsWithDestination_ThrowsInvalidOperationException()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            Action act = () => reader.ReadAllAnonymous(new { ID = 0, Name = "" }).ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_MissingColumnsWithDestination_ThrowsInvalidOperationExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            Func<Task> act = () => reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }).ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        public void ReadAllAnonymous_FromTypeOption_IsIgnored()
        {
            var fieldsOnly = People().CreateReader().ReadAllAnonymous(new { ID = 0, Name = "" }, new ReadOptions(fromTypeOption: FromTypeOption.PublicField)).ToList();
            var array = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader()
                .ReadAllAnonymous(new { ID = 0, Values = (int[])null }).ToList();

            fieldsOnly.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
            array.Should().ContainSingle().Which.Values.Should().Equal(1, 2);
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_FromTypeOption_IsIgnoredAsync()
        {
            var fieldsOnly = await People().CreateReader().ReadAllAnonymousAsync(new { ID = 0, Name = "" }, new ReadOptions(fromTypeOption: FromTypeOption.PublicField))
                .ToListCompat(TestContext.Current.CancellationToken);
            var array = await new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader()
                .ReadAllAnonymousAsync(new { ID = 0, Values = (int[])null }).ToListCompat(TestContext.Current.CancellationToken);

            fieldsOnly.Should().Equal(new { ID = 1, Name = "John" }, new { ID = 2, Name = "Kate" });
            array.Should().ContainSingle().Which.Values.Should().Equal(1, 2);
        }

        #endregion

        #region Case sensitivity and duplicates

        [Fact]
        public void ReadAllAnonymous_ColumnsInOtherCase_AreMatchedByDefault()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            var result = reader.ReadAllAnonymous(new { ID = 0, Name = "" }).ToList();

            result.Should().Equal(new { ID = 1, Name = "John" });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_ColumnsInOtherCase_AreMatchedByDefaultAsync()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }).ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(new { ID = 1, Name = "John" });
        }

        [Fact]
        public void ReadAllAnonymous_CaseSensitiveColumnsInOtherCase_AreNotMatched()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();
            var commonReader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            Action act = () => reader.ReadAllAnonymous(new { ID = 0, Name = "" }, new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();
            var common = commonReader.ReadAllAnonymous(new { ID = 0, Name = "" }, new ReadOptions(FieldsSelector.Common, true)).ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
            common.Should().Equal(new { ID = 0, Name = (string)null });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_CaseSensitiveColumnsInOtherCase_AreNotMatchedAsync()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();
            var commonReader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            Func<Task> act = () => reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }, new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);
            var common = await commonReader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }, new ReadOptions(FieldsSelector.Common, true))
                .ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
            common.Should().Equal(new { ID = 0, Name = (string)null });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadAllAnonymous_DuplicateColumnNames_ThrowsArgumentException(bool caseSensitive)
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Other").Row(1, "John", 2)
                .CreateInterfaceOnlyReader("ID", "Name", "ID");

            Action act = () => reader.ReadAllAnonymous(new { ID = 0, Name = "" }, new ReadOptions(caseSensitiveFieldsMatching: caseSensitive)).ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadAllAnonymousAsync_DuplicateColumnNames_ThrowsArgumentExceptionAsync(bool caseSensitive)
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Other").Row(1, "John", 2)
                .CreateInterfaceOnlyReader("ID", "Name", "ID");

            Func<Task> act = () => reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }, new ReadOptions(caseSensitiveFieldsMatching: caseSensitive))
                .ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Fact]
        public void ReadAllAnonymous_ParametersDifferingByCase_ThrowsArgumentException()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            Action act = () => reader.ReadAllAnonymous(new CaseConflictConstructor(0, 0)).ToList();

            act.Should().ThrowExactly<ArgumentException>().WithMessage("Destination object field names has been duplicated");
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_ParametersDifferingByCase_ThrowsArgumentExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            Func<Task> act = () => reader.ReadAllAnonymousAsync(new CaseConflictConstructor(0, 0)).ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Destination object field names has been duplicated");
        }

        [Fact]
        public void ReadAllAnonymous_CaseSensitiveParametersDifferingByCase_MapsBoth()
        {
            var reader = new ReaderTable().Column<int>("value").Column<int>("Value").Row(1, 2).CreateReader();

            var result = reader.ReadAllAnonymous(new CaseConflictConstructor(0, 0), new ReadOptions(caseSensitiveFieldsMatching: true)).ToList();

            result.Should().ContainSingle();
            result[0].Lower.Should().Be(1);
            result[0].Upper.Should().Be(2);
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_CaseSensitiveParametersDifferingByCase_MapsBothAsync()
        {
            var reader = new ReaderTable().Column<int>("value").Column<int>("Value").Row(1, 2).CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new CaseConflictConstructor(0, 0), new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].Lower.Should().Be(1);
            result[0].Upper.Should().Be(2);
        }

        #endregion

        #region Conversions and nulls

        [Fact]
        public void ReadAllAnonymous_Values_AreConverted()
        {
            var reader = new ReaderTable().Column<int>("Number").Column<string>("Color").Column<int>("Age").Column<string>("Name")
                .Row(42, "Green", null, null)
                .CreateReader();

            var result = reader.ReadAllAnonymous(new { Number = 0L, Color = Color.Red, Age = (int?)0, Name = "" }).ToList();

            result.Should().Equal(new { Number = 42L, Color = Color.Green, Age = (int?)null, Name = (string)null });
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_Values_AreConvertedAsync()
        {
            var reader = new ReaderTable().Column<int>("Number").Column<string>("Color").Column<int>("Age").Column<string>("Name")
                .Row(42, "Green", null, null)
                .CreateReader();

            var result = await reader.ReadAllAnonymousAsync(new { Number = 0L, Color = Color.Red, Age = (int?)0, Name = "" })
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().Equal(new { Number = 42L, Color = Color.Green, Age = (int?)null, Name = (string)null });
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAllAnonymous_DBNullIntoNonNullableParameter_ThrowsInvalidCastException()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(null, "John").CreateReader();

            Action act = () => reader.ReadAllAnonymous(new { ID = 0, Name = "" }).ToList();

            act.Should().ThrowExactly<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllAnonymousAsync_DBNullIntoNonNullableParameter_ThrowsInvalidCastExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(null, "John").CreateReader();

            Func<Task> act = () => reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" }).ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        #endregion

        #region Non-anonymous types

        [Fact]
        public void ReadAllAnonymous_TypeWithSeveralConstructors_UsesFirstConstructor()
        {
            var proto = new TwoConstructors("proto");
            var full = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").CreateReader();
            var nameOnly = new ReaderTable().Column<string>("Name").Row("Kate").CreateReader();
            var nameOnlyDestination = new ReaderTable().Column<string>("Name").Row("Kate").CreateReader();

            var fromFull = full.ReadAllAnonymous(proto).ToList();
            var fromNameOnly = nameOnly.ReadAllAnonymous(proto, new ReadOptions(fieldsSelector: FieldsSelector.Common)).ToList();
            Action destination = () => nameOnlyDestination.ReadAllAnonymous(proto).ToList();

            fromFull.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ID = 1, Name = "John", UsedConstructor = 1 });
            fromNameOnly.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ID = 0, Name = "Kate", UsedConstructor = 1 });
            destination.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_TypeWithSeveralConstructors_UsesFirstConstructorAsync()
        {
            var proto = new TwoConstructors("proto");
            var full = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").CreateReader();
            var nameOnly = new ReaderTable().Column<string>("Name").Row("Kate").CreateReader();

            var fromFull = await full.ReadAllAnonymousAsync(proto).ToListCompat(TestContext.Current.CancellationToken);
            var fromNameOnly = await nameOnly.ReadAllAnonymousAsync(proto, new ReadOptions(fieldsSelector: FieldsSelector.Common))
                .ToListCompat(TestContext.Current.CancellationToken);

            fromFull.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ID = 1, Name = "John", UsedConstructor = 1 });
            fromNameOnly.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ID = 0, Name = "Kate", UsedConstructor = 1 });
        }

        [Fact]
        [Trait("Quirk", "Q54")]
        public void ReadAllAnonymous_TypeWithoutPublicConstructor_ThrowsInvalidOperationException()
        {
            var reader = People().CreateReader();

            Action act = () => reader.ReadAllAnonymous(PrivateConstructor.Create()).ToList();

            act.Should().ThrowExactly<InvalidOperationException>().WithMessage("Sequence contains no elements");
        }

        [Fact]
        [Trait("Quirk", "Q54")]
        public async Task ReadAllAnonymousAsync_TypeWithoutPublicConstructor_ThrowsInvalidOperationExceptionAsync()
        {
            var reader = People().CreateReader();

            Func<Task> act = () => reader.ReadAllAnonymousAsync(PrivateConstructor.Create()).ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("Sequence contains no elements");
        }

        #endregion

        #region Laziness and options

        [Fact]
        public void ReadAllAnonymous_MappingError_IsThrownOnEnumeration()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = reader.ReadAllAnonymous(new { ID = 0, Name = "" });
            IEnumerator<object> enumerator = null;
            Action getEnumerator = () => enumerator = result.Cast<object>().GetEnumerator();
            Action moveNext = () => enumerator.MoveNext();

            getEnumerator.Should().NotThrow();
            moveNext.Should().ThrowExactly<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_MappingError_IsThrownOnFirstMoveNextAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = reader.ReadAllAnonymousAsync(new { ID = 0, Name = "" });

            using (var enumerator = result.GetEnumeratorCompat())
            {
                Func<Task> moveNext = () => enumerator.MoveNextAsync(TestContext.Current.CancellationToken);
                await moveNext.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
            }
        }

        [Fact]
        public void ReadAllAnonymous_SecondEnumeration_YieldsNothing()
        {
            var result = People().CreateReader().ReadAllAnonymous(new { ID = 0, Name = "" });

            var first = result.ToList();
            var second = result.ToList();

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadAllAnonymousAsync_SecondEnumeration_YieldsNothingAsync()
        {
            var result = People().CreateReader().ReadAllAnonymousAsync(new { ID = 0, Name = "" });

            var first = await result.ToListCompat(TestContext.Current.CancellationToken);
            var second = await result.ToListCompat(TestContext.Current.CancellationToken);

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        [Fact]
        [Trait("Quirk", "Q51")]
        public void ReadAllAnonymous_PassedOptions_UnsetValuesAreFilledOnEnumeration()
        {
            var options = new ReadOptions(caseSensitiveFieldsMatching: true);

            var result = People().CreateReader().ReadAllAnonymous(new { ID = 0, Name = "" }, options);
            var beforeEnumeration = options.FieldsSelector;
            result.ToList();

            beforeEnumeration.Should().BeNull();
            options.FieldsSelector.Should().Be(FieldsSelector.Destination);
            options.FromTypeOption.Should().Be(FromTypeOption.Default);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
        }

        [Fact]
        [Trait("Quirk", "Q51")]
        public async Task ReadAllAnonymousAsync_PassedOptions_UnsetValuesAreFilledOnFirstMoveNextAsync()
        {
            var options = new ReadOptions(caseSensitiveFieldsMatching: true);

            var result = People().CreateReader().ReadAllAnonymousAsync(new { ID = 0, Name = "" }, options);
            var beforeEnumeration = options.FieldsSelector;
            await result.ToListCompat(TestContext.Current.CancellationToken);

            beforeEnumeration.Should().BeNull();
            options.FieldsSelector.Should().Be(FieldsSelector.Destination);
            options.FromTypeOption.Should().Be(FromTypeOption.Default);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
        }

        #endregion
    }
}
