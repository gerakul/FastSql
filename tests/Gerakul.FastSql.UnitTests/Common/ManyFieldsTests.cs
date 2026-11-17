using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Name matching has a way for a few names (they are compared with each other, positions are kept on the
    // stack) and a way for many. The other tests use small models; these take the way for many.
    public class ManyFieldsTests
    {
        private const int Count = 70;

        public class Wide
        {
            public int P00 { get; set; }
            public int P01 { get; set; }
            public int P02 { get; set; }
            public int P03 { get; set; }
            public int P04 { get; set; }
            public int P05 { get; set; }
            public int P06 { get; set; }
            public int P07 { get; set; }
            public int P08 { get; set; }
            public int P09 { get; set; }
            public int P10 { get; set; }
            public int P11 { get; set; }
            public int P12 { get; set; }
            public int P13 { get; set; }
            public int P14 { get; set; }
            public int P15 { get; set; }
            public int P16 { get; set; }
            public int P17 { get; set; }
            public int P18 { get; set; }
            public int P19 { get; set; }
            public int P20 { get; set; }
            public int P21 { get; set; }
            public int P22 { get; set; }
            public int P23 { get; set; }
            public int P24 { get; set; }
            public int P25 { get; set; }
            public int P26 { get; set; }
            public int P27 { get; set; }
            public int P28 { get; set; }
            public int P29 { get; set; }
            public int P30 { get; set; }
            public int P31 { get; set; }
            public int P32 { get; set; }
            public int P33 { get; set; }
            public int P34 { get; set; }
            public int P35 { get; set; }
            public int P36 { get; set; }
            public int P37 { get; set; }
            public int P38 { get; set; }
            public int P39 { get; set; }
            public int P40 { get; set; }
            public int P41 { get; set; }
            public int P42 { get; set; }
            public int P43 { get; set; }
            public int P44 { get; set; }
            public int P45 { get; set; }
            public int P46 { get; set; }
            public int P47 { get; set; }
            public int P48 { get; set; }
            public int P49 { get; set; }
            public int P50 { get; set; }
            public int P51 { get; set; }
            public int P52 { get; set; }
            public int P53 { get; set; }
            public int P54 { get; set; }
            public int P55 { get; set; }
            public int P56 { get; set; }
            public int P57 { get; set; }
            public int P58 { get; set; }
            public int P59 { get; set; }
            public int P60 { get; set; }
            public int P61 { get; set; }
            public int P62 { get; set; }
            public int P63 { get; set; }
            public int P64 { get; set; }
            public int P65 { get; set; }
            public int P66 { get; set; }
            public int P67 { get; set; }
            public int P68 { get; set; }
            public int P69 { get; set; }
        }

        private static ReaderTable WideTable(Func<int, string> columnName)
        {
            var table = new ReaderTable();
            for (int i = 0; i < Count; i++)
            {
                table.Column<int>(columnName(i));
            }

            return table.Row(Enumerable.Range(0, Count).Select(x => (object)(x * 10)).ToArray());
        }

        private static int[] Values(Wide item)
        {
            return typeof(Wide).GetProperties().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => (int)x.GetValue(item)).ToArray();
        }

        [Fact]
        public void ReadAll_SeventyMembers_MapsEachFieldToItsMember()
        {
            var item = WideTable(i => $"P{i:00}").CreateReader().ReadAll<Wide>().Single();

            Values(item).Should().Equal(Enumerable.Range(0, Count).Select(x => x * 10));
        }

        [Fact]
        public void ReadAll_SeventyFieldsInAnotherOrderAndCase_MapsEachFieldToItsMember()
        {
            // the fields come in reverse order and in lower case
            var table = new ReaderTable();
            for (int i = Count - 1; i >= 0; i--)
            {
                table.Column<int>($"p{i:00}");
            }

            table.Row(Enumerable.Range(0, Count).Select(x => (object)((Count - 1 - x) * 10)).ToArray());

            var item = table.CreateReader().ReadAll<Wide>().Single();

            Values(item).Should().Equal(Enumerable.Range(0, Count).Select(x => x * 10));
        }

        [Fact]
        public void ReadAll_SeventyFieldsCaseSensitive_LowerCaseFieldsMatchNothing()
        {
            var reader = WideTable(i => $"p{i:00}").CreateReader();

            Action act = () => reader.ReadAll<Wide>(new ReadOptions(FieldsSelector.Destination, true)).Count();
            var item = WideTable(i => $"p{i:00}").CreateReader().ReadAll<Wide>(new ReadOptions(FieldsSelector.Common, true)).Single();

            act.Should().Throw<InvalidOperationException>();
            Values(item).Should().AllBeEquivalentTo(0);
        }

        [Fact]
        public void ReadAll_ManyFieldsWithADuplicate_Throws()
        {
            // names that differ only by case are duplicates when names are matched regardless of case
            var table = WideTable(i => i == Count - 1 ? "p00" : $"P{i:00}");

            Action ignoringCase = () => table.CreateReader().ReadAll<Wide>(new ReadOptions(FieldsSelector.Common)).Count();
            var caseSensitive = table.CreateReader().ReadAll<Wide>(new ReadOptions(FieldsSelector.Common, true)).Single();

            ignoringCase.Should().Throw<ArgumentException>().WithMessage("The reader has more than one field named '*'*");
            caseSensitive.P00.Should().Be(0);
            caseSensitive.P68.Should().Be(680);
            caseSensitive.P69.Should().Be(0);
        }

        [Fact]
        public void ReadAllAnonymous_ManyFields_FindsTheParameters()
        {
            var item = WideTable(i => $"P{i:00}").CreateReader()
                .ReadAllAnonymous(new { p69 = 0, P00 = 0, P35 = 0 }, new ReadOptions(FieldsSelector.Destination)).Single();

            item.Should().Be(new { p69 = 690, P00 = 0, P35 = 350 });
        }

        [Fact]
        public void CreateSimple_TwentyParameters_NamesThemByPosition()
        {
            var context = new SqlServerSpec().CreateCapturingContext();

            context.CreateSimple("select 1", Enumerable.Range(0, 20).Select(x => (object)x).ToArray());

            var parameters = context.LastCommand.Parameters.Cast<DbParameter>().ToArray();
            parameters.Select(x => x.ParameterName).Should().Equal(Enumerable.Range(0, 20).Select(x => "@p" + x));
            parameters.Select(x => x.Value).Should().Equal(Enumerable.Range(0, 20).Select(x => (object)x));
        }

        [Fact]
        public void CompileMapped_ManyParameters_MapsThemAndFindsDuplicates()
        {
            var provider = new SqlServerSpec().CreateProvider();
            var context = new SqlServerSpec().CreateCapturingContext(provider);
            var names = Enumerable.Range(0, Count).Select(x => $"@P{x:00}").ToList();
            var value = new Wide() { P00 = 1, P69 = 2 };

            var command = provider.CommandCompilator.CompileMapped<Wide>("select 1", names);
            context.CreateMapped(command, value);
            Action duplicate = () => provider.CommandCompilator.CompileMapped<Wide>("select 1", names.Append("@p00").ToList());
            Action unknown = () => provider.CommandCompilator.CompileMapped<Wide>("select 1", names.Append("@P70").ToList());

            var parameters = context.LastCommand.Parameters.Cast<DbParameter>().ToArray();
            parameters.Select(x => x.ParameterName).Should().Equal(names);
            parameters[0].Value.Should().Be(1);
            parameters[69].Value.Should().Be(2);
            duplicate.Should().Throw<ArgumentException>().WithMessage("Parameter '*' is listed more than once*");
            unknown.Should().Throw<ArgumentException>().WithMessage("No field of Wide matches the parameter '@P70' (Parameter 'parameters')");
        }
    }
}
