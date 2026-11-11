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
    // ReadOptions.FieldsSelector compares the reader columns (source) with the destination members:
    // properties and fields for ReadAll<T>, constructor parameters for ReadAllAnonymous.
    // The destination is always (ID, Name); the reader columns vary.
    public class FieldsSelectorTests
    {
        public class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        public enum Columns
        {
            ExactMatch,     // ID, Name
            ExtraColumn,    // ID, Name, Phone
            ExtraMember,    // ID
            NoOverlap       // Phone, Age
        }

        private const string DestinationMessage = "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";
        private const string SourceMessage = "FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination";
        private const string BothMessage = "FieldsSelector has value FieldsSelector.Both, but source fields do not match with destination fields";

        private static readonly MappingReadMethod[] methods =
        {
            MappingReadMethod.ReadAll,
            MappingReadMethod.ReadAllAsync,
            MappingReadMethod.ReadAllAnonymous,
            MappingReadMethod.ReadAllAnonymousAsync
        };

        // selector, columns, expected message (null when the rows are mapped)
        private static readonly object[][] matrix =
        {
            new object[] { FieldsSelector.Destination, Columns.ExactMatch, null },
            new object[] { FieldsSelector.Destination, Columns.ExtraColumn, null },
            new object[] { FieldsSelector.Destination, Columns.ExtraMember, DestinationMessage },
            new object[] { FieldsSelector.Destination, Columns.NoOverlap, DestinationMessage },

            new object[] { FieldsSelector.Source, Columns.ExactMatch, null },
            new object[] { FieldsSelector.Source, Columns.ExtraColumn, SourceMessage },
            new object[] { FieldsSelector.Source, Columns.ExtraMember, null },
            new object[] { FieldsSelector.Source, Columns.NoOverlap, SourceMessage },

            new object[] { FieldsSelector.Common, Columns.ExactMatch, null },
            new object[] { FieldsSelector.Common, Columns.ExtraColumn, null },
            new object[] { FieldsSelector.Common, Columns.ExtraMember, null },
            new object[] { FieldsSelector.Common, Columns.NoOverlap, null },

            new object[] { FieldsSelector.Both, Columns.ExactMatch, null },
            new object[] { FieldsSelector.Both, Columns.ExtraColumn, BothMessage },
            new object[] { FieldsSelector.Both, Columns.ExtraMember, BothMessage },
            new object[] { FieldsSelector.Both, Columns.NoOverlap, BothMessage },
        };

        public static TheoryData<MappingReadMethod> Methods => new TheoryData<MappingReadMethod>(methods);

        public static TheoryData<MappingReadMethod, FieldsSelector, Columns> MappedCases
        {
            get
            {
                var data = new TheoryData<MappingReadMethod, FieldsSelector, Columns>();
                foreach (var method in methods)
                {
                    foreach (var row in matrix.Where(x => x[2] == null))
                    {
                        data.Add(method, (FieldsSelector)row[0], (Columns)row[1]);
                    }
                }

                return data;
            }
        }

        public static TheoryData<MappingReadMethod, FieldsSelector, Columns, string> RejectedCases
        {
            get
            {
                var data = new TheoryData<MappingReadMethod, FieldsSelector, Columns, string>();
                foreach (var method in methods)
                {
                    foreach (var row in matrix.Where(x => x[2] != null))
                    {
                        data.Add(method, (FieldsSelector)row[0], (Columns)row[1], (string)row[2]);
                    }
                }

                return data;
            }
        }

        private static ReaderTable CreateTable(Columns columns, bool withRows = true)
        {
            var table = new ReaderTable();
            switch (columns)
            {
                case Columns.ExactMatch:
                    table.Column<int>("ID").Column<string>("Name");
                    return withRows ? table.Row(1, "John").Row(2, "Kate") : table;
                case Columns.ExtraColumn:
                    table.Column<int>("ID").Column<string>("Name").Column<string>("Phone");
                    return withRows ? table.Row(1, "John", "111").Row(2, "Kate", "222") : table;
                case Columns.ExtraMember:
                    table.Column<int>("ID");
                    return withRows ? table.Row(1).Row(2) : table;
                case Columns.NoOverlap:
                    table.Column<string>("Phone").Column<int>("Age");
                    return withRows ? table.Row("111", 30).Row("222", 40) : table;
                default:
                    throw new ArgumentOutOfRangeException(nameof(columns));
            }
        }

        private static IEnumerable<Person> ExpectedRows(Columns columns)
        {
            switch (columns)
            {
                case Columns.ExactMatch:
                case Columns.ExtraColumn:
                    return new[] { new Person() { ID = 1, Name = "John" }, new Person() { ID = 2, Name = "Kate" } };
                case Columns.ExtraMember:
                    return new[] { new Person() { ID = 1 }, new Person() { ID = 2 } };
                case Columns.NoOverlap:
                    return new[] { new Person(), new Person() };
                default:
                    throw new ArgumentOutOfRangeException(nameof(columns));
            }
        }

        // options == null calls the overloads without ReadOptions
        private static async Task<List<Person>> Read(MappingReadMethod method, ReaderTable table, ReadOptions options)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using (var reader = table.CreateReader())
            {
                switch (method)
                {
                    case MappingReadMethod.ReadAll:
                        return (options == null ? reader.ReadAll<Person>() : reader.ReadAll<Person>(options)).ToList();
                    case MappingReadMethod.ReadAllAsync:
                        return await (options == null ? reader.ReadAllAsync<Person>() : reader.ReadAllAsync<Person>(options))
                            .ToListCompat(cancellationToken);
                    case MappingReadMethod.ReadAllAnonymous:
                        var proto = new { ID = 0, Name = (string)null };
                        return (options == null ? reader.ReadAllAnonymous(proto) : reader.ReadAllAnonymous(proto, options))
                            .Select(x => new Person() { ID = x.ID, Name = x.Name }).ToList();
                    case MappingReadMethod.ReadAllAnonymousAsync:
                        var asyncProto = new { ID = 0, Name = (string)null };
                        var anonymous = await (options == null ? reader.ReadAllAnonymousAsync(asyncProto) : reader.ReadAllAnonymousAsync(asyncProto, options))
                            .ToListCompat(cancellationToken);
                        return anonymous.Select(x => new Person() { ID = x.ID, Name = x.Name }).ToList();
                    default:
                        throw new ArgumentOutOfRangeException(nameof(method));
                }
            }
        }

        [Theory]
        [MemberData(nameof(MappedCases))]
        public async Task Read_SelectorAcceptsColumns_MapsCommonMembers(MappingReadMethod method, FieldsSelector selector, Columns columns)
        {
            var result = await Read(method, CreateTable(columns), new ReadOptions(fieldsSelector: selector));

            result.Should().BeEquivalentTo(ExpectedRows(columns), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(RejectedCases))]
        public async Task Read_SelectorRejectsColumns_ThrowsInvalidOperationException(MappingReadMethod method, FieldsSelector selector, Columns columns,
            string message)
        {
            Func<Task> act = () => Read(method, CreateTable(columns), new ReadOptions(fieldsSelector: selector));

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(message);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_SelectorRejectsColumnsOfEmptyReader_ThrowsInvalidOperationException(MappingReadMethod method)
        {
            Func<Task> act = () => Read(method, CreateTable(Columns.ExtraMember, withRows: false), new ReadOptions(fieldsSelector: FieldsSelector.Destination));

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_WithoutOptions_UsesDestination(MappingReadMethod method)
        {
            var mapped = await Read(method, CreateTable(Columns.ExtraColumn), null);
            Func<Task> act = () => Read(method, CreateTable(Columns.ExtraMember), null);

            mapped.Should().BeEquivalentTo(ExpectedRows(Columns.ExtraColumn), o => o.WithStrictOrdering());
            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_UnsetSelector_UsesDestination(MappingReadMethod method)
        {
            var mapped = await Read(method, CreateTable(Columns.ExtraColumn), new ReadOptions());
            Func<Task> act = () => Read(method, CreateTable(Columns.ExtraMember), new ReadOptions());

            mapped.Should().BeEquivalentTo(ExpectedRows(Columns.ExtraColumn), o => o.WithStrictOrdering());
            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(DestinationMessage);
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Read_UndefinedSelector_ThrowsArgumentException(MappingReadMethod method)
        {
            Func<Task> zero = () => Read(method, CreateTable(Columns.ExactMatch), new ReadOptions(fieldsSelector: (FieldsSelector)0));
            Func<Task> five = () => Read(method, CreateTable(Columns.ExactMatch), new ReadOptions(fieldsSelector: (FieldsSelector)5));

            await zero.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Unknown FieldsSelector. Value: 0");
            await five.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("Unknown FieldsSelector. Value: 5");
        }
    }
}
