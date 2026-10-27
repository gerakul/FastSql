using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;
using static Gerakul.FastSql.UnitTests.Common.ReadAllTests;

namespace Gerakul.FastSql.UnitTests.Common
{
    // ReadAllAsync<T> twins of ReadAllTests (same models), and the behavior of the async enumerators returned by
    // all ReadAll*Async methods: the IDataReader-only path, cancellation (Q04), disposal, Current, empty readers.
    public class ReadAllAsyncTests
    {
        private const string DestinationMessage = "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";
        private const string SourceMessage = "FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination";

        public enum AsyncMethod
        {
            ReadAllOfT,
            ReadAllObjects,
            ReadAllAnonymous,
            ReadAllFirstColumn,
            ReadAllFirstColumnOfT
        }

        public static TheoryData<AsyncMethod> AsyncMethods => new TheoryData<AsyncMethod>(Enum.GetValues<AsyncMethod>());

        // the enumerator of any ReadAll*Async result, with Current boxed
        private sealed class Cursor : IDisposable
        {
            private readonly Func<Task<bool>> moveNext;
            private readonly Func<object> current;
            private readonly Action dispose;

            private Cursor(Func<Task<bool>> moveNext, Func<object> current, Action dispose)
            {
                this.moveNext = moveNext;
                this.current = current;
                this.dispose = dispose;
            }

            public static Cursor From<T>(CompatAsyncEnumerator<T> enumerator)
            {
                return new Cursor(enumerator.MoveNextAsync, () => enumerator.Current, enumerator.Dispose);
            }

            public object Current => current();

            public Task<bool> MoveNextAsync()
            {
                return moveNext();
            }

            public void Dispose()
            {
                dispose();
            }
        }

        private static ReaderTable People()
        {
            return new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate");
        }

        private static Cursor OpenCursor(AsyncMethod method, IDataReader reader, CancellationToken cancellationToken)
        {
            switch (method)
            {
                case AsyncMethod.ReadAllOfT:
                    return Cursor.From(reader.ReadAllAsync<Person>().GetEnumeratorCompat(cancellationToken));
                case AsyncMethod.ReadAllObjects:
                    return Cursor.From(reader.ReadAllAsync().GetEnumeratorCompat(cancellationToken));
                case AsyncMethod.ReadAllAnonymous:
                    return Cursor.From(reader.ReadAllAnonymousAsync(new { ID = 0, Name = (string)null }).GetEnumeratorCompat(cancellationToken));
                case AsyncMethod.ReadAllFirstColumn:
                    return Cursor.From(reader.ReadAllFirstColumnAsync().GetEnumeratorCompat(cancellationToken));
                case AsyncMethod.ReadAllFirstColumnOfT:
                    return Cursor.From(reader.ReadAllFirstColumnAsync<int>().GetEnumeratorCompat(cancellationToken));
                default:
                    throw new ArgumentOutOfRangeException(nameof(method));
            }
        }

        private static List<object> ReadSync(AsyncMethod method, IDataReader reader)
        {
            switch (method)
            {
                case AsyncMethod.ReadAllOfT:
                    return reader.ReadAll<Person>().Cast<object>().ToList();
                case AsyncMethod.ReadAllObjects:
                    return reader.ReadAll().Cast<object>().ToList();
                case AsyncMethod.ReadAllAnonymous:
                    return reader.ReadAllAnonymous(new { ID = 0, Name = (string)null }).Cast<object>().ToList();
                case AsyncMethod.ReadAllFirstColumn:
                    return reader.ReadAllFirstColumn().Cast<object>().ToList();
                case AsyncMethod.ReadAllFirstColumnOfT:
                    return reader.ReadAllFirstColumn<int>().Cast<object>().ToList();
                default:
                    throw new ArgumentOutOfRangeException(nameof(method));
            }
        }

        private static async Task<List<object>> ReadToEndAsync(AsyncMethod method, IDataReader reader)
        {
            var list = new List<object>();
            using (var cursor = OpenCursor(method, reader, TestContext.Current.CancellationToken))
            {
                while (await cursor.MoveNextAsync())
                {
                    list.Add(cursor.Current);
                }
            }

            return list;
        }

        #region Enumerator behavior of all ReadAll*Async methods

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_DbDataReader_ReturnsSameAsSyncAsync(AsyncMethod method)
        {
            var expected = ReadSync(method, People().CreateReader());

            var result = await ReadToEndAsync(method, People().CreateReader());

            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_InterfaceOnlyReader_ReturnsSameAsSyncAsync(AsyncMethod method)
        {
            var expected = ReadSync(method, People().CreateReader());
            var reader = People().CreateInterfaceOnlyReader();

            var result = await ReadToEndAsync(method, reader);

            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
            reader.ReadCalls.Should().Be(3);
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_EmptyReader_MoveNextReturnsFalseAsync(AsyncMethod method)
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").CreateReader();

            using (var cursor = OpenCursor(method, reader, TestContext.Current.CancellationToken))
            {
                (await cursor.MoveNextAsync()).Should().BeFalse();
            }
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_CurrentBeforeFirstAndAfterLastRow_IsDefaultAsync(AsyncMethod method)
        {
            var expectedDefault = method == AsyncMethod.ReadAllFirstColumnOfT ? (object)0 : null;

            using (var cursor = OpenCursor(method, People().CreateReader(), TestContext.Current.CancellationToken))
            {
                var beforeFirst = cursor.Current;
                while (await cursor.MoveNextAsync())
                {
                }

                beforeFirst.Should().Be(expectedDefault);
                cursor.Current.Should().Be(expectedDefault);
            }
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        [Trait("Quirk", "Q04")]
        public async Task ReadAsync_CancelledTokenBeforeFirstRow_MoveNextReturnsFalseWithoutReadingAsync(AsyncMethod method)
        {
            var reader = People().CreateInterfaceOnlyReader();

            using (var cursor = OpenCursor(method, reader, new CancellationToken(true)))
            {
                (await cursor.MoveNextAsync()).Should().BeFalse();
            }

            reader.ReadCalls.Should().Be(0);
        }

        // The built-in IAsyncEnumerator<T> gets its token once, when it is created, so a token cancelled after the first
        // row stays cancelled: the legacy enumerator could go on with a live token on the next call, this one cannot.
        [Theory]
        [MemberData(nameof(AsyncMethods))]
        [Trait("Quirk", "Q04")]
        public async Task ReadAsync_TokenCancelledAfterFirstRow_MoveNextReturnsFalseWithoutReadingAsync(AsyncMethod method)
        {
            var reader = People().CreateReader();

            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
            using (var cursor = OpenCursor(method, reader, cancellation.Token))
            {
                (await cursor.MoveNextAsync()).Should().BeTrue();
                cancellation.Cancel();

                (await cursor.MoveNextAsync()).Should().BeFalse();
                (await cursor.MoveNextAsync()).Should().BeFalse();
            }

            reader.Read().Should().BeTrue();
            reader.GetInt32(0).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_DisposedEnumerator_MoveNextReturnsFalseAndReaderStaysOpenAsync(AsyncMethod method)
        {
            var reader = People().CreateReader();
            var cursor = OpenCursor(method, reader, TestContext.Current.CancellationToken);

            (await cursor.MoveNextAsync()).Should().BeTrue();
            cursor.Dispose();

            (await cursor.MoveNextAsync()).Should().BeFalse();
            reader.IsClosed.Should().BeFalse();
            reader.Read().Should().BeTrue();
            reader.GetInt32(0).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(AsyncMethods))]
        public async Task ReadAsync_SecondEnumerator_YieldsNothingAsync(AsyncMethod method)
        {
            var reader = People().CreateReader();

            var first = await ReadToEndAsync(method, reader);
            var second = await ReadToEndAsync(method, reader);

            first.Should().HaveCount(2);
            second.Should().BeEmpty();
        }

        #endregion

        #region ReadAllAsync<T>: members

        [Fact]
        public async Task ReadAllAsync_PropertiesAndFields_MapsBothAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).Row(2, "Kate", null, null)
                .CreateReader();

            var result = await reader.ReadAllAsync<FieldsAndProperties>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[]
            {
                new FieldsAndProperties() { ID = 1, Name = "John", Phone = "111", Age = 40 },
                new FieldsAndProperties() { ID = 2, Name = "Kate" }
            }, o => o.WithStrictOrdering());
        }

        [Fact]
        public async Task ReadAllAsync_WithoutOptions_MapsLikeWithOptionsAsync()
        {
            var withoutOptions = await People().CreateReader().ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);
            var withNullOptions = await People().CreateReader().ReadAllAsync<Person>(null).ToListCompat(TestContext.Current.CancellationToken);

            withoutOptions.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" }, new Person() { ID = 2, Name = "Kate" } },
                o => o.WithStrictOrdering());
            withNullOptions.Should().BeEquivalentTo(withoutOptions, o => o.WithStrictOrdering());
        }

        [Fact]
        public async Task ReadAllAsync_ColumnOrderDiffersFromMemberOrder_MapsByNameAsync()
        {
            var reader = new ReaderTable().Column<string>("Name").Column<int>("ID").Row("John", 1).CreateReader();

            var result = await reader.ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public async Task ReadAllAsync_InheritedMembers_AreMappedAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Code").Column<string>("Name").Row(1, "A1", "John").CreateReader();

            var result = await reader.ReadAllAsync<DerivedEntity>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].ID.Should().Be(1);
            result[0].Code.Should().Be("A1");
            result[0].Name.Should().Be("John");
        }

        [Fact]
        public async Task ReadAllAsync_StaticAndNonPublicMembers_AreNotDestinationMembersAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int>("Count").Column<string>("Secret").Row(1, 5, "s").CreateReader();
            var sourceReader = new ReaderTable().Column<int>("ID").Column<int>("Count").Column<string>("Secret").Row(1, 5, "s").CreateReader();

            var result = await reader.ReadAllAsync<WithHiddenMembers>().ToListCompat(TestContext.Current.CancellationToken);
            Func<Task> source = () => sourceReader.ReadAllAsync<WithHiddenMembers>(new ReadOptions(fieldsSelector: FieldsSelector.Source))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].ID.Should().Be(1);
            result[0].Secret.Should().BeNull();
            WithHiddenMembers.Count.Should().Be(0);
            await source.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(SourceMessage);
        }

        [Fact]
        [Trait("Fixed", "Q120")]
        public async Task ReadAllAsync_Struct_SetsFieldsAndPropertiesAsync()
        {
            var result = await People().CreateReader().ReadAllAsync<PersonStruct>().ToListCompat(TestContext.Current.CancellationToken);

            result.Select(x => x.ID).Should().Equal(1, 2);
            result.Select(x => x.Name).Should().Equal("John", "Kate");
        }

        [Fact]
        public async Task ReadAllAsync_PrivateSetter_IsSetAsync()
        {
            var result = await People().CreateReader().ReadAllAsync<WithPrivateSetter>().ToListCompat(TestContext.Current.CancellationToken);

            result.Select(x => x.ID).Should().Equal(1, 2);
        }

        [Fact]
        [Trait("Fixed", "Q13")]
        public async Task ReadAllAsync_GetOnlyPropertyWithoutColumn_IsNotADestinationAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = await reader.ReadAllAsync<WithGetOnly>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Fixed", "Q13")]
        public async Task ReadAllAsync_GetOnlyPropertyWithColumn_IgnoresColumnAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Computed").Row(1, "x").CreateReader();
            var sourceReader = new ReaderTable().Column<int>("ID").Column<string>("Computed").Row(1, "x").CreateReader();

            var result = await reader.ReadAllAsync<WithGetOnly>().ToListCompat(TestContext.Current.CancellationToken);
            Func<Task> source = () => sourceReader.ReadAllAsync<WithGetOnly>(new ReadOptions(fieldsSelector: FieldsSelector.Source))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.Computed.Should().Be("computed");
            await source.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(SourceMessage);
        }

        [Fact]
        [Trait("Fixed", "Q13")]
        public async Task ReadAllAsync_GetOnlyPropertyWithCommonSelector_IsSkippedAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = await reader.ReadAllAsync<WithGetOnly>(new ReadOptions(fieldsSelector: FieldsSelector.Common))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Fixed", "Q53")]
        public async Task ReadAllAsync_IndexerWithoutColumn_IsNotADestinationAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            var result = await reader.ReadAllAsync<WithIndexer>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Fixed", "Q53")]
        public async Task ReadAllAsync_IndexerWithItemColumn_IgnoresColumnAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Item").Row(1, "x").CreateReader();

            var result = await reader.ReadAllAsync<WithIndexer>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.ID.Should().Be(1);
        }

        #endregion

        #region ReadAllAsync<T>: FromTypeOption

        [Fact]
        public async Task ReadAllAsync_PublicFieldOption_MapsOnlyFieldsAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).CreateReader();

            var result = await reader.ReadAllAsync<FieldsAndProperties>(new ReadOptions(fromTypeOption: FromTypeOption.PublicField))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { ID = 1, Name = "John" } });
        }

        [Fact]
        public async Task ReadAllAsync_PublicPropertyOption_MapsOnlyPropertiesAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(1, "John", "111", 40).CreateReader();

            var result = await reader.ReadAllAsync<FieldsAndProperties>(new ReadOptions(fromTypeOption: FromTypeOption.PublicProperty))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new FieldsAndProperties() { Phone = "111", Age = 40 } });
        }

        [Fact]
        public async Task ReadAllAsync_DefaultOption_SkipsArrayMemberAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();

            var result = await reader.ReadAllAsync<WithArray>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.Values.Should().BeNull();
        }

        [Fact]
        public async Task ReadAllAsync_CollectionOption_MapsArrayColumnAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();

            var result = await reader.ReadAllAsync<WithArray>(new ReadOptions(fromTypeOption: FromTypeOption.All))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.Values.Should().Equal(1, 2);
        }

        [Fact]
        public async Task ReadAllAsync_CollectionOptionAlone_MapsNoMembersAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<int[]>("Values").Row(1, new[] { 1, 2 }).CreateReader();

            var result = await reader.ReadAllAsync<WithArray>(new ReadOptions(fromTypeOption: FromTypeOption.Collection))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].ID.Should().Be(0);
            result[0].Values.Should().BeNull();
        }

        [Fact]
        [Trait("Fixed", "Q52")]
        public async Task ReadAllAsync_ArrayColumnIntoInterfaceMember_AssignsArrayAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string[]>("Tags").Row(1, new[] { "a", "b" }).CreateReader();

            var result = await reader.ReadAllAsync<WithEnumerable>(new ReadOptions(fromTypeOption: FromTypeOption.All))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle().Which.Tags.Should().Equal("a", "b");
        }

        [Fact]
        public async Task ReadAllAsync_DefaultOption_MapsByteArrayAndStringMembersAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<byte[]>("Data").Column<string>("Name")
                .Row(1, new byte[] { 1, 2 }, "John").CreateReader();

            var result = await reader.ReadAllAsync<WithBinary>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].Data.Should().Equal(1, 2);
            result[0].Name.Should().Be("John");
        }

        #endregion

        #region ReadAllAsync<T>: case sensitivity and duplicates

        [Fact]
        public async Task ReadAllAsync_ColumnsInOtherCase_AreMatchedByDefaultAsync()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            var result = await reader.ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public async Task ReadAllAsync_CaseSensitiveColumnsInOtherCase_AreNotMatchedAsync()
        {
            var reader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();
            var commonReader = new ReaderTable().Column<int>("id").Column<string>("NAME").Row(1, "John").CreateReader();

            Func<Task> act = () => reader.ReadAllAsync<Person>(new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);
            var common = await commonReader.ReadAllAsync<Person>(new ReadOptions(FieldsSelector.Common, true))
                .ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
            common.Should().BeEquivalentTo(new[] { new Person() });
        }

        [Fact]
        public async Task ReadAllAsync_CaseSensitiveColumnsInSameCase_AreMatchedAsync()
        {
            var result = await People().CreateReader().ReadAllAsync<Person>(new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Select(x => x.Name).Should().Equal("John", "Kate");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadAllAsync_DuplicateColumnNames_ThrowsArgumentExceptionAsync(bool caseSensitive)
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Other").Row(1, "John", 2)
                .CreateInterfaceOnlyReader("ID", "Name", "ID");

            Func<Task> act = () => reader.ReadAllAsync<Person>(new ReadOptions(caseSensitiveFieldsMatching: caseSensitive))
                .ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Fact]
        public async Task ReadAllAsync_ColumnNamesDifferingByCase_ThrowsArgumentExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("name").Row(1, "John", "john").CreateReader();

            Func<Task> act = () => reader.ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Reader field names has been duplicated");
        }

        [Fact]
        public async Task ReadAllAsync_CaseSensitiveColumnNamesDifferingByCase_MapsExactMatchAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("name").Row(1, "John", "john").CreateReader();

            var result = await reader.ReadAllAsync<Person>(new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new Person() { ID = 1, Name = "John" } });
        }

        [Fact]
        public async Task ReadAllAsync_FieldAndPropertyDifferingByCase_ThrowsArgumentExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            Func<Task> act = () => reader.ReadAllAsync<CaseConflict>().ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Destination object field names has been duplicated");
        }

        [Fact]
        public async Task ReadAllAsync_CaseSensitiveFieldAndPropertyDifferingByCase_MapsBothAsync()
        {
            var reader = new ReaderTable().Column<int>("value").Column<int>("Value").Row(1, 2).CreateReader();

            var result = await reader.ReadAllAsync<CaseConflict>(new ReadOptions(caseSensitiveFieldsMatching: true))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].value.Should().Be(1);
            result[0].Value.Should().Be(2);
        }

        [Fact]
        public async Task ReadAllAsync_FieldAndPropertyDifferingByCaseWithPropertyOption_MapsPropertyAsync()
        {
            var reader = new ReaderTable().Column<int>("Value").Row(2).CreateReader();

            var result = await reader.ReadAllAsync<CaseConflict>(new ReadOptions(fromTypeOption: FromTypeOption.PublicProperty))
                .ToListCompat(TestContext.Current.CancellationToken);

            result.Should().ContainSingle();
            result[0].value.Should().Be(0);
            result[0].Value.Should().Be(2);
        }

        #endregion

        #region ReadAllAsync<T>: nulls and field conversions

        [Fact]
        public async Task ReadAllAsync_DBNullIntoReferenceAndNullableMembers_SetsNullAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age").Column<DateTime>("StartWorking")
                .Column<byte[]>("Data").Column<string>("NameField").Column<int>("AgeField")
                .Row(1, null, null, null, null, null, null)
                .CreateReader();

            var result = await reader.ReadAllAsync<Nullables>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[] { new Nullables() { ID = 1 } });
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllAsync_DBNullIntoNonNullableProperty_ThrowsInvalidCastExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(null, "John").CreateReader();

            Func<Task> act = () => reader.ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public async Task ReadAllAsync_DBNullIntoNonNullableField_ThrowsInvalidCastExceptionAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<string>("Phone").Column<int>("Age")
                .Row(null, "John", "111", 40).CreateReader();

            Func<Task> act = () => reader.ReadAllAsync<FieldsAndProperties>().ToListCompat(TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<InvalidCastException>().WithMessage("Object cannot be cast from DBNull to other types.");
        }

        [Fact]
        public async Task ReadAllAsync_FieldValues_AreConvertedAsync()
        {
            var reader = new ReaderTable().Column<int>("Number").Column<string>("Color").Column<int>("NullableColor")
                .Row(42, "Green", 1)
                .Row(43, "Red", null)
                .CreateReader();

            var result = await reader.ReadAllAsync<FieldConversions>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(new[]
            {
                new FieldConversions() { Number = 42L, Color = Color.Green, NullableColor = Color.Red },
                new FieldConversions() { Number = 43L, Color = Color.Red, NullableColor = null }
            }, o => o.WithStrictOrdering());
        }

        #endregion

        #region ReadAllAsync<T>: laziness and reader state

        [Fact]
        public async Task ReadAllAsync_MappingError_IsThrownOnFirstMoveNextAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            CompatAsyncEnumerator<Person> enumerator = null;
            Action create = () => enumerator = reader.ReadAllAsync<Person>().GetEnumeratorCompat(TestContext.Current.CancellationToken);

            create.Should().NotThrow();
            using (enumerator)
            {
                Func<Task> moveNext = () => enumerator.MoveNextAsync();
                await moveNext.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
            }
        }

        [Fact]
        [Trait("Quirk", "Q04")]
        public async Task ReadAllAsync_MappingErrorWithCancelledToken_MoveNextReturnsFalseAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Row(1).CreateReader();

            using (var enumerator = reader.ReadAllAsync<Person>().GetEnumeratorCompat(new CancellationToken(true)))
            {
                (await enumerator.MoveNextAsync()).Should().BeFalse();
            }
        }

        [Fact]
        public async Task ReadAllAsync_ConversionErrorInLaterRow_IsThrownWhenRowIsReachedAsync()
        {
            var reader = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(null, "Kate").CreateReader();

            using (var enumerator = reader.ReadAllAsync<Person>().GetEnumeratorCompat(TestContext.Current.CancellationToken))
            {
                (await enumerator.MoveNextAsync()).Should().BeTrue();
                enumerator.Current.Name.Should().Be("John");

                Func<Task> moveNext = () => enumerator.MoveNextAsync();
                await moveNext.Should().ThrowExactlyAsync<InvalidCastException>();
            }
        }

        [Fact]
        public async Task ReadAllAsync_PartialEnumeration_NextEnumerationContinuesFromReaderPositionAsync()
        {
            var result = new ReaderTable().Column<int>("ID").Column<string>("Name").Row(1, "John").Row(2, "Kate").Row(3, "Mike")
                .CreateReader().ReadAllAsync<Person>();

            var first = await result.TakeCompat(1, TestContext.Current.CancellationToken);
            var rest = await result.ToListCompat(TestContext.Current.CancellationToken);

            first.Select(x => x.ID).Should().Equal(1);
            rest.Select(x => x.ID).Should().Equal(2, 3);
        }

        [Fact]
        public async Task ReadAllAsync_FullEnumeration_LeavesReaderOpenAsync()
        {
            var reader = People().CreateReader();

            await reader.ReadAllAsync<Person>().ToListCompat(TestContext.Current.CancellationToken);

            reader.IsClosed.Should().BeFalse();
        }

        [Fact]
        [Trait("Fixed", "Q31")]
        public async Task ReadAllAsync_PassedOptions_AreNotModifiedAsync()
        {
            var options = new ReadOptions(caseSensitiveFieldsMatching: true);

            await People().CreateReader().ReadAllAsync<Person>(options).ToListCompat(TestContext.Current.CancellationToken);

            options.FieldsSelector.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
        }

        #endregion
    }
}
