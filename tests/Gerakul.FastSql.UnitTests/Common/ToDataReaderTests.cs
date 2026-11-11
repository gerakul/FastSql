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
    public class ToDataReaderTests
    {
        public enum SourceKind
        {
            Enumerable,
            AsyncEnumerable
        }

        private enum Status
        {
            Active = 1,
            Retired = 2
        }

        private class Row
        {
            public int Code = 0;
            public int ID { get; set; }
            public string Name { get; set; }
        }

        private class CaseVariants
        {
            public int id = 1;
            public int ID { get; set; }
        }

        private class NullableValue
        {
            public int? Value { get; set; }
            public string Name { get; set; }
        }

        private class NonNullableValue
        {
            public int Value { get; set; }
            public string Name { get; set; }
        }

        private class OtherTypeHidingBase
        {
            public int Value { get; set; }
        }

        private class OtherTypeHiding : OtherTypeHidingBase
        {
            public new string Value { get; set; }
        }

        private class Detailed
        {
            public long Big;
            public int ID { get; set; }
            public string Name { get; set; }
            public decimal? Amount { get; set; }
            public bool Flag { get; set; }
            public Guid Key { get; set; }
            public byte[] Data { get; set; }
            public DateTime Created { get; set; }
            public DateTimeOffset? Changed { get; set; }
            public TimeSpan Duration { get; set; }
            public Status Status { get; set; }
            public Status? PreviousStatus { get; set; }
        }

        private static Row[] CreateRows()
        {
            return new[]
            {
                new Row() { Code = 10, ID = 1, Name = "John" },
                new Row() { Code = 20, ID = 2, Name = "Kate" }
            };
        }

        private static Employee[] CreateEmployees()
        {
            return new[]
            {
                new Employee() { ID = 1, CompanyID = 1, Name = "Jonh Smith", Phone = "+1 (111) 111-1111", Age = 45, StartWorking = new DateTime(2010, 1, 15) },
                new Employee() { ID = 2, CompanyID = 2, Name = "Kate Donahue", Phone = null, Age = null, StartWorking = null }
            };
        }

        private static Detailed[] CreateDetailed()
        {
            return new[]
            {
                new Detailed()
                {
                    Big = 10000000000L,
                    ID = 1,
                    Name = "first",
                    Amount = 12.5m,
                    Flag = true,
                    Key = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
                    Data = new byte[] { 1, 2, 3 },
                    Created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                    Changed = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
                    Duration = TimeSpan.FromMinutes(90),
                    Status = Status.Retired,
                    PreviousStatus = Status.Active
                },
                new Detailed()
                {
                    ID = 2,
                    Name = null,
                    Amount = null,
                    Data = null,
                    Changed = null,
                    Status = Status.Active,
                    PreviousStatus = null
                }
            };
        }

        private static string[] Describe<T>(IEnumerable<FieldSettings<T>> settings)
        {
            return settings.Select(x => $"{x.Name}:{x.FieldType.Name}:{x.DataTypeName}:{x.DetermineNullByValue}").ToArray();
        }

        private static void ShouldMatchCase<T>(UniversalDataReader<T> reader, string name, bool caseSensitive)
        {
            var ordinal = reader.GetOrdinal(name);
            Func<int> lookUpOtherCase = () => reader.GetOrdinal(name.ToUpperInvariant());

            if (caseSensitive)
            {
                lookUpOtherCase.Should().Throw<IndexOutOfRangeException>();
            }
            else
            {
                lookUpOtherCase().Should().Be(ordinal);
            }
        }

        #region Overloads

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_NoArguments_UsesDefaultMembersCaseSensitive(SourceKind source)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader()
                : rows.ToAsyncEnumerableCompat().ToDataReader();

            Describe(reader.Settings).Should().Equal("Code:Int32:Int32:True", "ID:Int32:Int32:True", "Name:String:String:True");
            ShouldMatchCase(reader, "Name", true);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable, true)]
        [InlineData(SourceKind.Enumerable, false)]
        [InlineData(SourceKind.AsyncEnumerable, true)]
        [InlineData(SourceKind.AsyncEnumerable, false)]
        public void ToDataReader_CaseSensitivity_UsesDefaultMembers(SourceKind source, bool caseSensitive)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(caseSensitive)
                : rows.ToAsyncEnumerableCompat().ToDataReader(caseSensitive);

            Describe(reader.Settings).Should().Equal(Describe(FieldSettings.FromType<Row>()));
            ShouldMatchCase(reader, "Name", caseSensitive);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_SettingsArray_UsesGivenSettingsCaseSensitive(SourceKind source)
        {
            var rows = CreateRows();
            var settings = new[]
            {
                new FieldSettings<Row>("Name", typeof(string), x => x.Name),
                new FieldSettings<Row>("Number", typeof(int), x => x.ID)
            };
            var original = settings.ToArray();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(settings)
                : rows.ToAsyncEnumerableCompat().ToDataReader(settings);
            settings[0] = new FieldSettings<Row>("Other", typeof(int), x => x.Code);

            reader.Settings.Should().Equal(original);
            ShouldMatchCase(reader, "Name", true);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_SettingsAsSeparateArguments_UsesGivenSettingsCaseSensitive(SourceKind source)
        {
            var rows = CreateRows();
            var name = new FieldSettings<Row>("Name", typeof(string), x => x.Name);
            var number = new FieldSettings<Row>("Number", typeof(int), x => x.ID);

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(name, number)
                : rows.ToAsyncEnumerableCompat().ToDataReader(name, number);

            reader.Settings.Should().Equal(name, number);
            ShouldMatchCase(reader, "Name", true);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_EmptySettingsArray_GivesReaderWithoutColumns(SourceKind source)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(new FieldSettings<Row>[0])
                : rows.ToAsyncEnumerableCompat().ToDataReader(new FieldSettings<Row>[0]);

            reader.FieldCount.Should().Be(0);
            reader.Read().Should().BeTrue();
            reader.Read().Should().BeTrue();
            reader.Read().Should().BeFalse();
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_SettingsSequence_IsCaseSensitiveByDefault(SourceKind source)
        {
            var rows = CreateRows();
            var settings = new List<FieldSettings<Row>>()
            {
                new FieldSettings<Row>("Name", typeof(string), x => x.Name)
            };

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(settings)
                : rows.ToAsyncEnumerableCompat().ToDataReader(settings);

            reader.Settings.Should().Equal(settings);
            ShouldMatchCase(reader, "Name", true);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable, true)]
        [InlineData(SourceKind.Enumerable, false)]
        [InlineData(SourceKind.AsyncEnumerable, true)]
        [InlineData(SourceKind.AsyncEnumerable, false)]
        public void ToDataReader_SettingsSequenceAndCaseSensitivity_UsesGivenSettings(SourceKind source, bool caseSensitive)
        {
            var rows = CreateRows();
            var settings = new List<FieldSettings<Row>>()
            {
                new FieldSettings<Row>("Name", typeof(string), x => x.Name),
                new FieldSettings<Row>("Number", typeof(int), x => x.ID)
            };

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(settings, caseSensitive)
                : rows.ToAsyncEnumerableCompat().ToDataReader(settings, caseSensitive);

            reader.Settings.Should().Equal(settings);
            ShouldMatchCase(reader, "Name", caseSensitive);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_FromTypeOption_UsesSelectedMembersCaseSensitive(SourceKind source)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(FromTypeOption.PublicField)
                : rows.ToAsyncEnumerableCompat().ToDataReader(FromTypeOption.PublicField);

            Describe(reader.Settings).Should().Equal("Code:Int32:Int32:True");
            ShouldMatchCase(reader, "Code", true);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable, true)]
        [InlineData(SourceKind.Enumerable, false)]
        [InlineData(SourceKind.AsyncEnumerable, true)]
        [InlineData(SourceKind.AsyncEnumerable, false)]
        public void ToDataReader_FromTypeOptionAndCaseSensitivity_UsesSelectedMembers(SourceKind source, bool caseSensitive)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader(FromTypeOption.PublicProperty, caseSensitive)
                : rows.ToAsyncEnumerableCompat().ToDataReader(FromTypeOption.PublicProperty, caseSensitive);

            Describe(reader.Settings).Should().Equal("ID:Int32:Int32:True", "Name:String:String:True");
            ShouldMatchCase(reader, "Name", caseSensitive);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_ReadsSourceValues(SourceKind source)
        {
            var rows = CreateRows();

            var reader = source == SourceKind.Enumerable
                ? rows.ToDataReader()
                : rows.ToAsyncEnumerableCompat().ToDataReader();

            var values = new List<object>();
            while (reader.Read())
            {
                values.Add(reader["Code"]);
                values.Add(reader["ID"]);
                values.Add(reader["Name"]);
            }

            values.Should().Equal(10, 1, "John", 20, 2, "Kate");
        }

        #endregion

        #region Duplicate names

        [Fact]
        public void ToDataReader_MembersDifferingOnlyByCase_WorkCaseSensitive()
        {
            var reader = new[] { new CaseVariants() { ID = 2 } }.ToDataReader();
            reader.Read();

            reader["id"].Should().Be(1);
            reader["ID"].Should().Be(2);
        }

        [Fact]
        public void ToDataReader_MembersDifferingOnlyByCaseCaseInsensitive_ThrowsArgumentException()
        {
            var values = new[] { new CaseVariants() };

            Action act = () => values.ToDataReader(false);

            act.Should().Throw<ArgumentException>().WithMessage("An item with the same key has already been added. Key: id");
        }

        [Fact]
        [Trait("Fixed", "Q48")]
        public void ToDataReader_MemberHiddenWithOtherType_UsesHidingMember()
        {
            var values = new[] { new OtherTypeHiding() { Value = "new" } };

            var reader = values.ToDataReader();
            reader.Read();

            Describe(reader.Settings).Should().Equal("Value:String:String:True");
            reader.GetValue(0).Should().Be("new");
        }

        #endregion

        #region Round trip

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_ReadAll_ReturnsEqualObjects(SourceKind source)
        {
            var employees = CreateEmployees();

            var reader = source == SourceKind.Enumerable
                ? employees.ToDataReader()
                : employees.ToAsyncEnumerableCompat().ToDataReader();
            var result = reader.ReadAll<Employee>().ToList();

            result.Should().BeEquivalentTo(employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public async Task ToDataReader_ReadAllAsync_ReturnsEqualObjectsAsync(SourceKind source)
        {
            var employees = CreateEmployees();

            var reader = source == SourceKind.Enumerable
                ? employees.ToDataReader()
                : employees.ToAsyncEnumerableCompat().ToDataReader();
            var result = await reader.ReadAllAsync<Employee>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public void ToDataReader_ReadAllOfManyTypes_ReturnsEqualObjects(SourceKind source)
        {
            var values = CreateDetailed();

            var reader = source == SourceKind.Enumerable
                ? values.ToDataReader()
                : values.ToAsyncEnumerableCompat().ToDataReader();
            var result = reader.ReadAll<Detailed>().ToList();

            result.Should().BeEquivalentTo(values, o => o.WithStrictOrdering());
            result[0].Created.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Theory]
        [InlineData(SourceKind.Enumerable)]
        [InlineData(SourceKind.AsyncEnumerable)]
        public async Task ToDataReader_ReadAllAsyncOfManyTypes_ReturnsEqualObjectsAsync(SourceKind source)
        {
            var values = CreateDetailed();

            var reader = source == SourceKind.Enumerable
                ? values.ToDataReader()
                : values.ToAsyncEnumerableCompat().ToDataReader();
            var result = await reader.ReadAllAsync<Detailed>().ToListCompat(TestContext.Current.CancellationToken);

            result.Should().BeEquivalentTo(values, o => o.WithStrictOrdering());
            result[0].Created.Kind.Should().Be(DateTimeKind.Utc);
        }

        // UniversalDataReader.GetValue returns null, not DBNull, for a null member (Q47), so the mapping code sets null
        // into a non-nullable member instead of failing like it does for DBNull (Q12)
        [Fact]
        [Trait("Quirk", "Q112")]
        public void ToDataReader_ReadAllNullIntoNonNullableMember_SetsDefaultValue()
        {
            var values = new[] { new NullableValue() { Value = null, Name = "John" } };

            var result = values.ToDataReader().ReadAll<NonNullableValue>().ToList();

            result.Should().BeEquivalentTo(new[] { new NonNullableValue() { Value = 0, Name = "John" } });
        }

        [Fact]
        [Trait("Quirk", "Q112")]
        public void ToDataReader_ReadAllAnonymousNullIntoNonNullableParameter_PassesDefaultValue()
        {
            var values = new[] { new NullableValue() { Value = null, Name = "John" } };

            var result = values.ToDataReader().ReadAllAnonymous(new { Value = 1, Name = "" }).ToList();

            result.Should().BeEquivalentTo(new[] { new { Value = 0, Name = "John" } });
        }

        [Fact]
        [Trait("Quirk", "Q112")]
        public void ToDataReader_ReadAllFirstColumnNullIntoNonNullableType_ThrowsNullReferenceException()
        {
            var reader = new int?[] { null }.ToDataReader(new FieldSettings<int?>("Value"));

            Action act = () => reader.ReadAllFirstColumn<int>().ToList();

            act.Should().ThrowExactly<NullReferenceException>();
        }

        #endregion
    }
}
