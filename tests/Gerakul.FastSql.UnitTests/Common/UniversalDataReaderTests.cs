using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    public class UniversalDataReaderTests
    {
        public enum SourceKind
        {
            Enumerable,
            LegacyAsyncEnumerable
        }

        private class Row
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        private class AllTypes
        {
            public bool Boolean { get; set; }
            public byte Byte { get; set; }
            public char Char { get; set; }
            public DateTime DateTime { get; set; }
            public decimal Decimal { get; set; }
            public double Double { get; set; }
            public float Float { get; set; }
            public Guid Guid { get; set; }
            public short Int16 { get; set; }
            public int Int32 { get; set; }
            public long Int64 { get; set; }
            public string String { get; set; }
            public int? NullableInt32 { get; set; }
        }

        private class Binary
        {
            public byte[] Bytes { get; set; }
            public char[] Chars { get; set; }
            public string Text { get; set; }
        }

        // counts the calls the reader makes on the source enumerator
        private class TrackingEnumerable : IEnumerable<Row>
        {
            private readonly Row[] rows;

            public int GetEnumeratorCalls;
            public TrackingEnumerator LastEnumerator;

            public TrackingEnumerable(params Row[] rows)
            {
                this.rows = rows;
            }

            public IEnumerator<Row> GetEnumerator()
            {
                GetEnumeratorCalls++;
                LastEnumerator = new TrackingEnumerator(rows);
                return LastEnumerator;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class TrackingEnumerator : IEnumerator<Row>
        {
            private readonly Row[] rows;
            private int index = -1;

            public int MoveNextCalls;
            public int DisposeCalls;

            public TrackingEnumerator(Row[] rows)
            {
                this.rows = rows;
            }

            public Row Current
            {
                get
                {
                    return rows[index];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public bool MoveNext()
            {
                MoveNextCalls++;
                if (index + 1 < rows.Length)
                {
                    index++;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                DisposeCalls++;
            }
        }

        private static Row[] CreateRows()
        {
            return new[]
            {
                new Row() { ID = 1, Name = "John", Age = 40 },
                new Row() { ID = 2, Name = null, Age = null },
                new Row() { ID = 3, Name = "Kate", Age = 25 }
            };
        }

        private static IEnumerable<Row> RowThenThrow()
        {
            yield return new Row() { ID = 1 };
            throw new InvalidOperationException("Source failed");
        }

        private static UniversalDataReader<T> CreateReader<T>(SourceKind source, IEnumerable<T> rows, IEnumerable<FieldSettings<T>> settings,
            bool caseSensitive = true)
        {
            return source == SourceKind.Enumerable
                ? new UniversalDataReader<T>(rows, settings, caseSensitive)
                : new UniversalDataReader<T>(rows.ToAsyncEnumerableCompat(), settings, caseSensitive);
        }

        private static UniversalDataReader<T> CreateReader<T>(SourceKind source, IEnumerable<T> rows, bool caseSensitive = true)
        {
            return CreateReader(source, rows, FieldSettings.FromType<T>(), caseSensitive);
        }

        private static UniversalDataReader<Row> CreateReader(bool caseSensitive = true)
        {
            return CreateReader(SourceKind.Enumerable, CreateRows(), caseSensitive);
        }

        private static List<object[]> ReadRows(DbDataReader reader)
        {
            var result = new List<object[]>();
            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                result.Add(values);
            }

            return result;
        }

        private static async Task<List<object[]>> ReadRowsAsync(DbDataReader reader)
        {
            var result = new List<object[]>();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                result.Add(values);
            }

            return result;
        }

        #region Construction

        [Theory]
        [InlineData(SourceKind.Enumerable, true)]
        [InlineData(SourceKind.Enumerable, false)]
        [InlineData(SourceKind.LegacyAsyncEnumerable, true)]
        [InlineData(SourceKind.LegacyAsyncEnumerable, false)]
        public void Constructor_StoresSettingsAndCaseSensitivity(SourceKind source, bool caseSensitive)
        {
            var settings = FieldSettings.FromType<Row>();

            var reader = CreateReader(source, CreateRows(), settings, caseSensitive);

            reader.Settings.Should().Equal(settings);
            Action lookup = () => reader.GetOrdinal("name");
            if (caseSensitive)
            {
                lookup.Should().Throw<KeyNotFoundException>();
            }
            else
            {
                reader.GetOrdinal("name").Should().Be(1);
            }
        }

        [Fact]
        public void Constructor_CopiesSettingsSequence()
        {
            var settings = new List<FieldSettings<Row>>(FieldSettings.FromType<Row>());

            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings);
            settings.RemoveAt(0);

            reader.FieldCount.Should().Be(3);
            reader.Settings.Should().HaveCount(3);
        }

        [Fact]
        public void Constructor_GetsSourceEnumeratorWithoutMovingIt()
        {
            var source = new TrackingEnumerable(CreateRows());

            var reader = new UniversalDataReader<Row>(source, FieldSettings.FromType<Row>(), true);

            source.GetEnumeratorCalls.Should().Be(1);
            source.LastEnumerator.MoveNextCalls.Should().Be(0);
            reader.Read();
            source.LastEnumerator.MoveNextCalls.Should().Be(1);
        }

        [Fact]
        public void Constructor_NullValues_ThrowsNullReferenceException()
        {
            Action act = () => new UniversalDataReader<Row>((IEnumerable<Row>)null, FieldSettings.FromType<Row>(), true);

            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            Action act = () => new UniversalDataReader<Row>(CreateRows(), null, true);

            act.Should().Throw<ArgumentNullException>().WithParameterName("source");
        }

        [Fact]
        public void Settings_ReturnsNewReadOnlyCollectionOverSameSettings()
        {
            var reader = CreateReader();

            var first = reader.Settings;
            var second = reader.Settings;

            second.Should().NotBeSameAs(first);
            second.Should().Equal(first);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public void Constructor_DuplicateNamesCaseInsensitive_ThrowsArgumentException(SourceKind source)
        {
            var settings = new[]
            {
                new FieldSettings<Row>("ID", typeof(int), x => x.ID),
                new FieldSettings<Row>("id", typeof(int), x => x.ID)
            };

            Action act = () => CreateReader(source, CreateRows(), settings, false);

            act.Should().Throw<ArgumentException>().WithMessage("An item with the same key has already been added. Key: id");
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public void Constructor_DuplicateNamesCaseSensitive_ThrowsArgumentException(SourceKind source)
        {
            var settings = new[]
            {
                new FieldSettings<Row>("ID", typeof(int), x => x.ID),
                new FieldSettings<Row>("ID", typeof(int), x => x.ID)
            };

            Action act = () => CreateReader(source, CreateRows(), settings, true);

            act.Should().Throw<ArgumentException>().WithMessage("An item with the same key has already been added. Key: ID");
        }

        [Fact]
        public void Constructor_NamesDifferingByCaseCaseSensitive_AreSeparateColumns()
        {
            var settings = new[]
            {
                new FieldSettings<Row>("ID", typeof(int), x => x.ID),
                new FieldSettings<Row>("id", typeof(string), x => x.Name)
            };

            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings, true);

            reader.GetOrdinal("ID").Should().Be(0);
            reader.GetOrdinal("id").Should().Be(1);
        }

        #endregion

        #region Metadata

        [Fact]
        public void Metadata_ComesFromSettings()
        {
            var settings = new[]
            {
                new FieldSettings<Row>("ID", typeof(int), x => x.ID),
                new FieldSettings<Row>("Name", "my type", typeof(string), x => x.Name),
                new FieldSettings<Row>("Age", typeof(int?), x => x.Age)
            };

            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings);

            reader.FieldCount.Should().Be(3);
            Enumerable.Range(0, 3).Select(reader.GetName).Should().Equal("ID", "Name", "Age");
            Enumerable.Range(0, 3).Select(reader.GetFieldType).Should().Equal(typeof(int), typeof(string), typeof(int?));
            Enumerable.Range(0, 3).Select(reader.GetDataTypeName).Should().Equal("Int32", "my type", "Nullable`1");
            reader.Depth.Should().Be(0);
        }

        [Fact]
        public void Metadata_DefaultSettings_UseClrTypeNames()
        {
            var reader = CreateReader();

            Enumerable.Range(0, 3).Select(reader.GetDataTypeName).Should().Equal("Int32", "String", "Nullable`1");
        }

        [Fact]
        public void GetName_OrdinalOutOfRange_ThrowsIndexOutOfRangeException()
        {
            var reader = CreateReader();

            Action act = () => reader.GetName(3);

            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void GetOrdinal_CaseSensitive_MatchesExactNameOnly()
        {
            var reader = CreateReader(true);

            reader.GetOrdinal("ID").Should().Be(0);
            reader.GetOrdinal("Name").Should().Be(1);
            reader.GetOrdinal("Age").Should().Be(2);
            Action act = () => reader.GetOrdinal("NAME");
            act.Should().Throw<KeyNotFoundException>().WithMessage("The given key 'NAME' was not present in the dictionary.");
        }

        [Fact]
        public void GetOrdinal_CaseInsensitive_IgnoresCase()
        {
            var reader = CreateReader(false);

            reader.GetOrdinal("id").Should().Be(0);
            reader.GetOrdinal("NAME").Should().Be(1);
            reader.GetOrdinal("aGe").Should().Be(2);
        }

        [Fact]
        public void GetOrdinal_CaseInsensitiveUnknownName_ThrowsKeyNotFoundExceptionWithLowerCaseName()
        {
            var reader = CreateReader(false);

            Action act = () => reader.GetOrdinal("Unknown");

            act.Should().Throw<KeyNotFoundException>().WithMessage("The given key 'unknown' was not present in the dictionary.");
        }

        #endregion

        #region Values

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public void Read_ReturnsEachRowThenFalse(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());

            var rows = ReadRows(reader);

            rows.Should().BeEquivalentTo(new[]
            {
                new object[] { 1, "John", 40 },
                new object[] { 2, null, null },
                new object[] { 3, "Kate", 25 }
            }, o => o.WithStrictOrdering());
            reader.Read().Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public async Task ReadAsync_ReturnsEachRowThenFalseAsync(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());

            var rows = await ReadRowsAsync(reader);

            rows.Should().BeEquivalentTo(new[]
            {
                new object[] { 1, "John", 40 },
                new object[] { 2, null, null },
                new object[] { 3, "Kate", 25 }
            }, o => o.WithStrictOrdering());
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public void Read_EmptySource_ReturnsFalse(SourceKind source)
        {
            var reader = CreateReader(source, new Row[0]);

            reader.Read().Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public async Task ReadAsync_EmptySource_ReturnsFalseAsync(SourceKind source)
        {
            var reader = CreateReader(source, new Row[0]);

            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        [Trait("Quirk", "Q41")]
        public async Task ReadAsync_CancelledToken_StillReadsAsync(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());

            var result = await reader.ReadAsync(new CancellationToken(true));

            result.Should().BeTrue();
            reader.GetValue(0).Should().Be(1);
        }

        // Record.Exception keeps the exception as thrown: the assertion library would look inside an AggregateException
        [Fact]
        public void Read_EnumerableSourceThrows_ThrowsOriginalException()
        {
            var reader = CreateReader(SourceKind.Enumerable, RowThenThrow());
            reader.Read();

            var exception = Record.Exception(() => reader.Read());

            exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Source failed");
        }

        [Fact]
        [Trait("Quirk", "Q42")]
        [Trait("UpgradeSensitive", "System.Interactive.Async 3.2 reports source exceptions through the MoveNext task")]
        public void Read_LegacyAsyncSourceThrows_ThrowsAggregateException()
        {
            var reader = CreateReader(SourceKind.LegacyAsyncEnumerable, RowThenThrow());
            reader.Read();

            var exception = Record.Exception(() => reader.Read());

            exception.Should().BeOfType<AggregateException>()
                .Which.InnerExceptions.Should().ContainSingle()
                .Which.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Source failed");
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public async Task ReadAsync_SourceThrows_ThrowsOriginalExceptionAsync(SourceKind source)
        {
            var reader = CreateReader(source, RowThenThrow());
            await reader.ReadAsync(TestContext.Current.CancellationToken);

            var exception = await Record.ExceptionAsync(() => reader.ReadAsync(TestContext.Current.CancellationToken));

            exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Source failed");
        }

        [Fact]
        [Trait("Quirk", "Q47")]
        public void GetValue_NullMember_ReturnsNullNotDBNull()
        {
            var reader = CreateReader();
            reader.Read();
            reader.Read();

            reader.GetValue(1).Should().BeNull();
            reader.GetValue(2).Should().BeNull();
            reader.IsDBNull(1).Should().BeTrue();
            reader.IsDBNull(2).Should().BeTrue();
            reader.IsDBNull(0).Should().BeFalse();
        }

        [Fact]
        [Trait("Quirk", "Q47")]
        public void IsDBNull_GetterReturnsDBNull_IsFalse()
        {
            var settings = new[] { new FieldSettings<Row>("Name", typeof(string), x => (object)x.Name ?? DBNull.Value) };
            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings);
            reader.Read();
            reader.Read();

            reader.GetValue(0).Should().Be(DBNull.Value);
            reader.IsDBNull(0).Should().BeFalse();
        }

        [Fact]
        public void IsDBNull_CustomPredicate_IsUsedAndValueIsStillReturned()
        {
            var settings = new[] { new FieldSettings<Row>("ID", typeof(int), x => x.ID, x => x.ID == 1) };
            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings);

            reader.Read();
            reader.IsDBNull(0).Should().BeTrue();
            reader.GetValue(0).Should().Be(1);
            reader.Read();
            reader.IsDBNull(0).Should().BeFalse();
        }

        [Fact]
        public async Task IsDBNullAsync_UsesIsNullGetterAsync()
        {
            var settings = new[] { new FieldSettings<Row>("ID", typeof(int), x => x.ID, x => x.ID == 1) };
            var reader = CreateReader(SourceKind.Enumerable, CreateRows(), settings);

            reader.Read();
            (await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken)).Should().BeTrue();
            reader.Read();
            (await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken)).Should().BeFalse();
        }

        [Fact]
        public void GetFieldValue_CastsValueExactly()
        {
            var reader = CreateReader();
            reader.Read();

            reader.GetFieldValue<int>(0).Should().Be(1);
            reader.GetFieldValue<int?>(2).Should().Be(40);
            Action act = () => reader.GetFieldValue<long>(0);
            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public async Task GetFieldValueAsync_CastsValueExactlyAsync()
        {
            var reader = CreateReader();
            reader.Read();

            (await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken)).Should().Be("John");
            Func<Task> act = () => reader.GetFieldValueAsync<long>(0, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<InvalidCastException>();
        }

        [Fact]
        public void GetValues_ShorterArray_FillsOnlyItsLength()
        {
            var reader = CreateReader();
            reader.Read();
            var values = new object[2];

            var count = reader.GetValues(values);

            count.Should().Be(2);
            values.Should().Equal(1, "John");
        }

        [Fact]
        public void GetValues_LongerArray_LeavesRestUntouched()
        {
            var reader = CreateReader();
            reader.Read();
            var values = new object[] { "a", "b", "c", "d", "e" };

            var count = reader.GetValues(values);

            count.Should().Be(3);
            values.Should().Equal(1, "John", 40, "d", "e");
        }

        [Fact]
        public void IndexerByOrdinal_ReturnsValue()
        {
            var reader = CreateReader();
            reader.Read();

            reader[0].Should().Be(1);
            reader[1].Should().Be("John");
            reader[2].Should().Be(40);
        }

        [Fact]
        public void IndexerByName_CaseSensitive_MatchesExactNameOnly()
        {
            var reader = CreateReader(true);
            reader.Read();

            reader["Name"].Should().Be("John");
            Func<object> act = () => reader["name"];
            act.Should().Throw<KeyNotFoundException>().WithMessage("The given key 'name' was not present in the dictionary.");
        }

        [Fact]
        public void IndexerByName_CaseInsensitive_IgnoresCase()
        {
            var reader = CreateReader(false);
            reader.Read();

            reader["NAME"].Should().Be("John");
            reader["age"].Should().Be(40);
        }

        [Fact]
        public void TypedGetters_ExactType_ReturnValues()
        {
            var guid = Guid.NewGuid();
            var date = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            var row = new AllTypes()
            {
                Boolean = true,
                Byte = 200,
                Char = 'z',
                DateTime = date,
                Decimal = 12.34m,
                Double = 1.5,
                Float = 2.5f,
                Guid = guid,
                Int16 = -3,
                Int32 = 100000,
                Int64 = 10000000000L,
                String = "text",
                NullableInt32 = 5
            };
            var reader = CreateReader(SourceKind.Enumerable, new[] { row });
            reader.Read();

            reader.GetBoolean(reader.GetOrdinal("Boolean")).Should().BeTrue();
            reader.GetByte(reader.GetOrdinal("Byte")).Should().Be(200);
            reader.GetChar(reader.GetOrdinal("Char")).Should().Be('z');
            reader.GetDateTime(reader.GetOrdinal("DateTime")).Should().Be(date);
            reader.GetDecimal(reader.GetOrdinal("Decimal")).Should().Be(12.34m);
            reader.GetDouble(reader.GetOrdinal("Double")).Should().Be(1.5);
            reader.GetFloat(reader.GetOrdinal("Float")).Should().Be(2.5f);
            reader.GetGuid(reader.GetOrdinal("Guid")).Should().Be(guid);
            reader.GetInt16(reader.GetOrdinal("Int16")).Should().Be(-3);
            reader.GetInt32(reader.GetOrdinal("Int32")).Should().Be(100000);
            reader.GetInt64(reader.GetOrdinal("Int64")).Should().Be(10000000000L);
            reader.GetString(reader.GetOrdinal("String")).Should().Be("text");
            reader.GetInt32(reader.GetOrdinal("NullableInt32")).Should().Be(5);
        }

        [Fact]
        public void GetInt64_Int32Value_ThrowsInvalidCastException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new AllTypes() { Int32 = 1 } });
            reader.Read();

            Action act = () => reader.GetInt64(reader.GetOrdinal("Int32"));

            act.Should().Throw<InvalidCastException>().WithMessage("Unable to cast object of type 'System.Int32' to type 'System.Int64'.");
        }

        [Fact]
        public void GetInt32_Int16Value_ThrowsInvalidCastException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new AllTypes() { Int16 = 1 } });
            reader.Read();

            Action act = () => reader.GetInt32(reader.GetOrdinal("Int16"));

            act.Should().Throw<InvalidCastException>().WithMessage("Unable to cast object of type 'System.Int16' to type 'System.Int32'.");
        }

        [Fact]
        public void GetString_Int32Value_ThrowsInvalidCastException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new AllTypes() { Int32 = 1 } });
            reader.Read();

            Action act = () => reader.GetString(reader.GetOrdinal("Int32"));

            act.Should().Throw<InvalidCastException>().WithMessage("Unable to cast object of type 'System.Int32' to type 'System.String'.");
        }

        [Fact]
        public void TypedGetters_NullValue_ValueTypeThrowsNullReferenceExceptionAndStringReturnsNull()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new AllTypes() { NullableInt32 = null, String = null } });
            reader.Read();

            Action act = () => reader.GetInt32(reader.GetOrdinal("NullableInt32"));

            act.Should().Throw<NullReferenceException>();
            reader.GetString(reader.GetOrdinal("String")).Should().BeNull();
        }

        [Theory]
        [InlineData(0L, 0, 4, 4, new byte[] { 1, 2, 3, 4 })]
        [InlineData(2L, 0, 4, 3, new byte[] { 3, 4, 5, 0 })]
        [InlineData(0L, 1, 2, 2, new byte[] { 0, 1, 2, 0 })]
        [InlineData(0L, 0, 10, 4, new byte[] { 1, 2, 3, 4 })]
        [InlineData(3L, 2, 10, 2, new byte[] { 0, 0, 4, 5 })]
        [InlineData(0L, 0, 0, 0, new byte[] { 0, 0, 0, 0 })]
        [InlineData(5L, 0, 4, 0, new byte[] { 0, 0, 0, 0 })]
        [InlineData(-1L, 0, 4, 0, new byte[] { 0, 0, 0, 0 })]
        [InlineData(0L, 4, 4, 0, new byte[] { 0, 0, 0, 0 })]
        [InlineData(0L, -1, 4, 0, new byte[] { 0, 0, 0, 0 })]
        public void GetBytes_CopiesWithinBounds(long fieldOffset, int bufferOffset, int length, long expectedCount, byte[] expectedBuffer)
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Bytes = new byte[] { 1, 2, 3, 4, 5 } } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();
            var buffer = new byte[4];

            var count = reader.GetBytes(0, fieldOffset, buffer, bufferOffset, length);

            count.Should().Be(expectedCount);
            buffer.Should().Equal(expectedBuffer);
        }

        [Theory]
        [InlineData(0L, 0, 4, 4, "abcd")]
        [InlineData(2L, 0, 4, 3, "cde\0")]
        [InlineData(0L, 1, 2, 2, "\0ab\0")]
        [InlineData(0L, 0, 10, 4, "abcd")]
        [InlineData(3L, 2, 10, 2, "\0\0de")]
        [InlineData(0L, 0, 0, 0, "\0\0\0\0")]
        [InlineData(5L, 0, 4, 0, "\0\0\0\0")]
        [InlineData(-1L, 0, 4, 0, "\0\0\0\0")]
        [InlineData(0L, 4, 4, 0, "\0\0\0\0")]
        [InlineData(0L, -1, 4, 0, "\0\0\0\0")]
        public void GetChars_CopiesWithinBounds(long fieldOffset, int bufferOffset, int length, long expectedCount, string expectedBuffer)
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Chars = "abcde".ToCharArray() } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();
            var buffer = new char[4];

            var count = reader.GetChars(1, fieldOffset, buffer, bufferOffset, length);

            count.Should().Be(expectedCount);
            new string(buffer).Should().Be(expectedBuffer);
        }

        [Fact]
        [Trait("Quirk", "Q43")]
        public void GetBytes_NullBuffer_ThrowsNullReferenceException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Bytes = new byte[] { 1, 2, 3 } } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();

            Action act = () => reader.GetBytes(0, 0, null, 0, 0);

            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        [Trait("Quirk", "Q43")]
        public void GetChars_NullBuffer_ThrowsNullReferenceException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Chars = "abc".ToCharArray() } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();

            Action act = () => reader.GetChars(1, 0, null, 0, 0);

            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        [Trait("Quirk", "Q43")]
        public void GetChars_StringValue_ThrowsInvalidCastException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Text = "abc" } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();

            Action act = () => reader.GetChars(2, 0, new char[3], 0, 3);

            act.Should().Throw<InvalidCastException>().WithMessage("Unable to cast object of type 'System.String' to type 'System.Char[]'.");
        }

        [Fact]
        public void GetBytes_StringValue_ThrowsInvalidCastException()
        {
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Text = "abc" } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();

            Action act = () => reader.GetBytes(2, 0, new byte[3], 0, 3);

            act.Should().Throw<InvalidCastException>().WithMessage("Unable to cast object of type 'System.String' to type 'System.Byte[]'.");
        }

        [Fact]
        public void GetStream_ReadsWholeByteArrayThroughGetBytes()
        {
            var bytes = Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray();
            var reader = CreateReader(SourceKind.Enumerable, new[] { new Binary() { Bytes = bytes } },
                FieldSettings.FromType<Binary>(FromTypeOption.All));
            reader.Read();

            using (var stream = reader.GetStream(0))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                copy.ToArray().Should().Equal(bytes);
            }
        }

        [Fact]
        [Trait("Quirk", "Q44")]
        public void GetValue_BeforeFirstRead_RunsGetterOnDefaultValue()
        {
            var scalars = new[] { 5 }.ToDataReader(new FieldSettings<int>("Value"));
            var objects = CreateReader();

            scalars.GetValue(0).Should().Be(0);
            Action act = () => objects.GetValue(0);
            act.Should().Throw<TargetException>().WithMessage("Non-static method requires a target.");
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        [Trait("Quirk", "Q44")]
        public void GetValue_AfterReadReturnedFalse_ReturnsLastRow(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());
            while (reader.Read())
            {
            }

            reader.GetValue(0).Should().Be(3);
            reader["Name"].Should().Be("Kate");
        }

        [Fact]
        [Trait("Quirk", "Q44")]
        public void GetValue_AfterClose_ReturnsCurrentRow()
        {
            var reader = CreateReader();
            reader.Read();

            reader.Close();

            reader.GetValue(0).Should().Be(1);
            reader.FieldCount.Should().Be(3);
            reader.GetName(1).Should().Be("Name");
        }

        #endregion

        #region Lifecycle

        [Fact]
        public void IsClosed_NewReader_IsFalse()
        {
            CreateReader().IsClosed.Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public void Close_SetsIsClosedAndReadThrows(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());
            reader.Read();

            reader.Close();

            reader.IsClosed.Should().BeTrue();
            Action act = () => reader.Read();
            act.Should().Throw<InvalidOperationException>().WithMessage("DataReader has been closed");
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.LegacyAsyncEnumerable)]
        public async Task Close_ThenReadAsync_ThrowsAsync(SourceKind source)
        {
            var reader = CreateReader(source, CreateRows());

            reader.Close();

            Func<Task> act = () => reader.ReadAsync(TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DataReader has been closed");
        }

        [Fact]
        public void Close_Twice_DoesNotThrow()
        {
            var reader = CreateReader();

            reader.Close();
            reader.Close();

            reader.IsClosed.Should().BeTrue();
        }

        [Fact]
        [Trait("Quirk", "Q01")]
        public void Close_ThroughDbDataReaderReference_DoesNotClose()
        {
            DbDataReader reader = CreateReader();

            reader.Close();

            reader.IsClosed.Should().BeFalse();
            reader.Read().Should().BeTrue();
        }

        [Fact]
        [Trait("Quirk", "Q01")]
        public void Close_ThroughIDataReaderReference_DoesNotClose()
        {
            IDataReader reader = CreateReader();

            reader.Close();

            reader.IsClosed.Should().BeFalse();
            reader.Read().Should().BeTrue();
        }

        [Fact]
        [Trait("Quirk", "Q01")]
        public async Task CloseAsync_DoesNotCloseAsync()
        {
            var reader = CreateReader();

            await reader.CloseAsync();

            reader.IsClosed.Should().BeFalse();
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        }

        [Fact]
        public void Dispose_Closes()
        {
            var reader = CreateReader();

            reader.Dispose();

            reader.IsClosed.Should().BeTrue();
            Action act = () => reader.Read();
            act.Should().Throw<InvalidOperationException>().WithMessage("DataReader has been closed");
        }

        [Fact]
        public void Dispose_ThroughIDisposableReference_Closes()
        {
            var reader = CreateReader();

            using (IDisposable disposable = reader)
            {
            }

            reader.IsClosed.Should().BeTrue();
        }

        [Fact]
        public async Task DisposeAsync_ClosesAsync()
        {
            var reader = CreateReader();

            await reader.DisposeAsync();

            reader.IsClosed.Should().BeTrue();
        }

        [Fact]
        [Trait("Quirk", "Q40")]
        public void Close_EnumerableSource_NeverDisposesSourceEnumerator()
        {
            var partial = new TrackingEnumerable(CreateRows());
            var full = new TrackingEnumerable(CreateRows());
            var partialReader = new UniversalDataReader<Row>(partial, FieldSettings.FromType<Row>(), true);
            var fullReader = new UniversalDataReader<Row>(full, FieldSettings.FromType<Row>(), true);

            partialReader.Read();
            partialReader.Close();
            while (fullReader.Read())
            {
            }
            fullReader.Dispose();

            partial.LastEnumerator.DisposeCalls.Should().Be(0);
            full.LastEnumerator.DisposeCalls.Should().Be(0);
        }

        [Fact]
        [Trait("Quirk", "Q40")]
        [Trait("UpgradeSensitive", "System.Interactive.Async 3.2 ToAsyncEnumerable pulls lazily and disposes the source only when disposed itself")]
        public void Close_PartiallyReadLegacyAsyncSource_DoesNotDisposeSourceEnumerator()
        {
            var source = new TrackingEnumerable(CreateRows());
            var reader = new UniversalDataReader<Row>(source.ToAsyncEnumerableCompat(), FieldSettings.FromType<Row>(), true);

            reader.Read();
            reader.Close();
            reader.Dispose();

            source.LastEnumerator.MoveNextCalls.Should().Be(1);
            source.LastEnumerator.DisposeCalls.Should().Be(0);
        }

        #endregion

        #region Not implemented and not supported

        [Fact]
        [Trait("Quirk", "Q02")]
        public void NextResult_ThrowsNotImplementedException()
        {
            var reader = CreateReader();

            Action act = () => reader.NextResult();

            act.Should().Throw<NotImplementedException>().WithMessage("The method or operation is not implemented.");
        }

        [Fact]
        [Trait("Quirk", "Q02")]
        public async Task NextResultAsync_ThrowsNotImplementedExceptionAsync()
        {
            var reader = CreateReader();

            Func<Task> act = () => reader.NextResultAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotImplementedException>();
        }

        [Fact]
        [Trait("Quirk", "Q02")]
        public void RecordsAffected_ThrowsNotImplementedException()
        {
            var reader = CreateReader();

            Func<int> act = () => reader.RecordsAffected;

            act.Should().Throw<NotImplementedException>().WithMessage("The method or operation is not implemented.");
        }

        [Fact]
        [Trait("Quirk", "Q02")]
        public void HasRows_ThrowsNotImplementedException()
        {
            var reader = CreateReader();

            Func<bool> act = () => reader.HasRows;

            act.Should().Throw<NotImplementedException>().WithMessage("The method or operation is not implemented.");
        }

        [Fact]
        [Trait("Quirk", "Q03")]
        public void GetEnumerator_EnumerableSource_ReturnsSourceEnumerator()
        {
            var source = new TrackingEnumerable(CreateRows());
            var reader = new UniversalDataReader<Row>(source, FieldSettings.FromType<Row>(), true);

            var enumerator = reader.GetEnumerator();

            enumerator.Should().BeSameAs(source.LastEnumerator);
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().BeOfType<Row>().Which.ID.Should().Be(1);
        }

        [Fact]
        [Trait("Quirk", "Q03")]
        public void GetEnumerator_EnumerableSource_SharesPositionWithRead()
        {
            var reader = CreateReader();
            reader.Read();

            reader.GetEnumerator().MoveNext();
            reader.Read();

            reader.GetValue(0).Should().Be(3);
        }

        [Fact]
        [Trait("Quirk", "Q03")]
        public void GetEnumerator_LegacyAsyncSource_ReturnsNull()
        {
            var reader = CreateReader(SourceKind.LegacyAsyncEnumerable, CreateRows());

            reader.GetEnumerator().Should().BeNull();
        }

        [Fact]
        [Trait("Quirk", "Q14")]
        public void GetSchemaTable_ThrowsNotSupportedException()
        {
            var reader = CreateReader();

            Action act = () => reader.GetSchemaTable();

            act.Should().Throw<NotSupportedException>().WithMessage("Specified method is not supported.");
        }

        [Fact]
        [Trait("Quirk", "Q14")]
        public async Task GetSchemaTableAsync_ThrowsNotSupportedExceptionAsync()
        {
            var reader = CreateReader();

            Func<Task> act = () => reader.GetSchemaTableAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("Specified method is not supported.");
        }

        [Fact]
        [Trait("Quirk", "Q14")]
        public void GetColumnSchema_ThrowsNotSupportedException()
        {
            var reader = CreateReader();

            Action act = () => reader.GetColumnSchema();

            reader.CanGetColumnSchema().Should().BeTrue();
            act.Should().Throw<NotSupportedException>().WithMessage("Specified method is not supported.");
        }

        [Fact]
        [Trait("Quirk", "Q14")]
        public async Task GetColumnSchemaAsync_ThrowsNotSupportedExceptionAsync()
        {
            var reader = CreateReader();

            Func<Task> act = () => reader.GetColumnSchemaAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("Specified method is not supported.");
        }

        #endregion
    }
}
