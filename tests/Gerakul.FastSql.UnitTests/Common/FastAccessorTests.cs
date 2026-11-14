using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // A type that is read or written a lot gets compiled accessors after its first 10,000 rows (FastAccessors).
    // They must do exactly what the accessors that go through reflection do, so the tests here use more rows
    // than that and compare the rows before the switch with the rows after it. The accessors are compiled on
    // the thread pool, so the tests wait for them at the threshold (WaitingAtTheThreshold). The state is kept
    // per type, for the whole process, so every test has model types of its own.
    public class FastAccessorTests
    {
        private const int Threshold = 10000;
        private const int Rows = 3 * Threshold;

        public enum Color
        {
            Red,
            Green,
            Blue
        }

        #region Reading into members

        public class AwkwardBase
        {
            public int Hidden { get; set; }
            public virtual int Virtual { get; set; }
            public int Inherited { get; set; }
        }

        public class Awkward : AwkwardBase
        {
            public int Field;
            public readonly int ReadonlyField;
            public int PrivateSet { get; private set; }
            public int InitOnly { get; init; }
            public new long Hidden { get; set; }
            public override int Virtual { get; set; }
            public int? NullableInt { get; set; }
            public Color Color { get; set; }
            public Color? NullableColor { get; set; }
            public object Any { get; set; }
            public string Name { get; set; }
            public decimal Converted { get; set; }

            public string Describe()
            {
                return $"{Field}|{ReadonlyField}|{PrivateSet}|{InitOnly}|{Hidden}|{Virtual}|{Inherited}|{NullableInt}|{Color}|{NullableColor}|{Any}|{Name}|{Converted}";
            }
        }

        private static ReaderTable AwkwardTable(int rows)
        {
            var table = new ReaderTable()
                .Column<int>("Field").Column<int>("ReadonlyField").Column<int>("PrivateSet").Column<int>("InitOnly")
                .Column<long>("Hidden").Column<int>("Virtual").Column<int>("Inherited").Column<int>("NullableInt")
                .Column<string>("Color").Column<int>("NullableColor").Column<Guid>("Any").Column<string>("Name")
                .Column<int>("Converted");

            for (int i = 0; i < rows; i++)
            {
                table.Row(i, i + 1, i + 2, i + 3, i + 4L, i + 5, i + 6, i % 3 == 0 ? null : (object)i,
                    ((Color)(i % 3)).ToString(), i % 2 == 0 ? null : (object)(i % 3), new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                    i % 5 == 0 ? null : "Name " + i, i);
            }

            return table;
        }

        private static string DescribeAwkward(int i)
        {
            return $"{i}|{i + 1}|{i + 2}|{i + 3}|{i + 4L}|{i + 5}|{i + 6}|{(i % 3 == 0 ? null : (int?)i)}|{(Color)(i % 3)}|"
                + $"{(i % 2 == 0 ? null : (Color?)(i % 3))}|{new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)}|{(i % 5 == 0 ? null : "Name " + i)}|{(decimal)i}";
        }

        [Fact]
        public void ReadAll_MoreRowsThanTheThreshold_MapsEveryRowTheSameWay()
        {
            var items = WaitingAtTheThreshold(AwkwardTable(Rows).CreateReader().ReadAll<Awkward>(), WaitForSetters<Awkward>).ToArray();

            items.Select(x => x.Describe()).Should().Equal(Enumerable.Range(0, Rows).Select(DescribeAwkward));
        }

        public struct AwkwardStruct
        {
            public int Field;
            public int Property { get; set; }
            public int InitOnly { get; init; }
            public int? NullableInt { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void ReadAll_Struct_MoreRowsThanTheThreshold_MapsEveryRowTheSameWay()
        {
            var table = new ReaderTable().Column<int>("Field").Column<int>("Property").Column<int>("InitOnly").Column<int>("NullableInt").Column<string>("Name");
            for (int i = 0; i < Rows; i++)
            {
                table.Row(i, i + 1, i + 2, i % 2 == 0 ? null : (object)i, "Name " + i);
            }

            var items = WaitingAtTheThreshold(table.CreateReader().ReadAll<AwkwardStruct>(), WaitForSetters<AwkwardStruct>).ToArray();

            items.Select(x => (x.Field, x.Property, x.InitOnly, x.NullableInt, x.Name)).Should()
                .Equal(Enumerable.Range(0, Rows).Select(i => (i, i + 1, i + 2, i % 2 == 0 ? null : (int?)i, "Name " + i)));
        }

        public class Compiled
        {
            public int Field;
            public readonly int ReadonlyField;
            public int Property { get; set; }
            public string Name { get; private set; }
        }

        [Fact]
        public void ReadAll_MoreRowsThanTheThreshold_CompilesTheSetters()
        {
            var table = new ReaderTable().Column<int>("Field").Column<int>("ReadonlyField").Column<int>("Property").Column<string>("Name");
            for (int i = 0; i < Threshold + 1; i++)
            {
                table.Row(i, i, i, "x");
            }

            table.CreateReader().ReadAll<Compiled>().Take(Threshold - 1).Count();
            var before = Setters<Compiled>();
            table.CreateReader().ReadAll<Compiled>().Count();
            WaitForSetters<Compiled>();
            var after = Setters<Compiled>();

            before.Values.Select(IsCompiled).Should().Equal(false, false, false, false);
            IsCompiled(after["Field"]).Should().BeTrue();
            IsCompiled(after["Property"]).Should().BeTrue();
            IsCompiled(after["Name"]).Should().BeTrue();

            // an expression tree cannot assign a readonly field, so reflection stays for it
            IsCompiled(after["ReadonlyField"]).Should().BeFalse();
        }

        public class ThrowingSetter
        {
            public int ID { get; set; }

            public int Value
            {
                get => 0;
                set
                {
                    if (value < 0)
                    {
                        throw new InvalidOperationException("Negative value " + value);
                    }
                }
            }
        }

        // the row with the negative value is the last one
        private static ReaderTable ThrowingSetterTable(int rows)
        {
            var table = new ReaderTable().Column<int>("ID").Column<int>("Value");
            for (int i = 0; i < rows; i++)
            {
                table.Row(i, i == rows - 1 ? -1 : i);
            }

            return table;
        }

        [Fact]
        [Trait("Fixed", "I28")]
        public void ReadAll_SetterThrows_ThrowsTheSettersExceptionBeforeAndAfterTheThreshold()
        {
            Action before = () => ThrowingSetterTable(5).CreateReader().ReadAll<ThrowingSetter>().Count();
            Action after = () => WaitingAtTheThreshold(ThrowingSetterTable(Rows).CreateReader().ReadAll<ThrowingSetter>(), WaitForSetters<ThrowingSetter>).Count();

            before.Should().Throw<InvalidOperationException>().WithMessage("Negative value -1");
            after.Should().Throw<InvalidOperationException>().WithMessage("Negative value -1");
        }

        public class NotNullable
        {
            public int ID { get; set; }
            public int Value { get; set; }
        }

        // the row with the null is the last one
        private static ReaderTable NullInLastRow(int rows)
        {
            var table = new ReaderTable().Column<int>("ID").Column<int>("Value");
            for (int i = 0; i < rows; i++)
            {
                table.Row(i, i == rows - 1 ? null : (object)i);
            }

            return table;
        }

        [Fact]
        [Trait("Quirk", "Q12")]
        public void ReadAll_DBNullIntoNonNullableMember_ThrowsBeforeAndAfterTheThreshold()
        {
            Action before = () => NullInLastRow(5).CreateReader().ReadAll<NotNullable>().Count();
            Action after = () => WaitingAtTheThreshold(NullInLastRow(Rows).CreateReader().ReadAll<NotNullable>(), WaitForSetters<NotNullable>).Count();

            before.Should().Throw<InvalidCastException>().WithMessage("Cannot read field '*' (*) into *: Object cannot be cast from DBNull to other types.");
            after.Should().Throw<InvalidCastException>().WithMessage("Cannot read field '*' (*) into *: Object cannot be cast from DBNull to other types.");
        }

        public class NullableSource
        {
            public int ID { get; set; }
            public int? Value { get; set; }
        }

        public class NotNullableTarget
        {
            public int ID { get; set; }
            public int Value { get; set; }
        }

        [Fact]
        [Trait("Quirk", "Q112")]
        public void ReadAll_NullOfAUniversalDataReaderIntoNonNullableMember_GivesTheDefaultValueBeforeAndAfterTheThreshold()
        {
            var source = Enumerable.Range(0, Rows).Select(i => new NullableSource() { ID = i, Value = i % 2 == 0 ? null : (int?)i });

            var items = WaitingAtTheThreshold(
                WaitingAtTheThreshold(source, WaitForGetters<NullableSource>).ToDataReader().ReadAll<NotNullableTarget>(),
                WaitForSetters<NotNullableTarget>).ToArray();

            items.Select(x => (x.ID, x.Value)).Should().Equal(Enumerable.Range(0, Rows).Select(i => (i, i % 2 == 0 ? 0 : i)));
        }

        #endregion

        #region Reading through a constructor

        public record Positional(int ID, string Name, int? Age, Color Color, decimal Converted);

        private static ReaderTable PositionalTable(int rows)
        {
            var table = new ReaderTable().Column<int>("ID").Column<string>("Name").Column<int>("Age").Column<string>("Color").Column<int>("Converted");
            for (int i = 0; i < rows; i++)
            {
                table.Row(i, i % 5 == 0 ? null : "Name " + i, i % 3 == 0 ? null : (object)i, ((Color)(i % 3)).ToString(), i);
            }

            return table;
        }

        private static IEnumerable<Positional> ExpectedPositional(int rows)
        {
            return Enumerable.Range(0, rows).Select(i => new Positional(i, i % 5 == 0 ? null : "Name " + i, i % 3 == 0 ? null : (int?)i, (Color)(i % 3), i));
        }

        [Fact]
        public void ReadAllAnonymous_Record_MoreRowsThanTheThreshold_CreatesEveryRowTheSameWay()
        {
            var items = WaitingAtTheThreshold(PositionalTable(Rows).CreateReader().ReadAllAnonymous(default(Positional)), WaitForConstructor<Positional>).ToArray();

            items.Should().Equal(ExpectedPositional(Rows));
        }

        [Fact]
        public void ReadAllAnonymous_AnonymousType_MoreRowsThanTheThreshold_CreatesEveryRowTheSameWay()
        {
            var proto = new { ID = 0, Name = "", Age = (int?)null, Color = Color.Red, Converted = 0m };

            var items = WaitingAtTheThreshold(PositionalTable(Rows).CreateReader().ReadAllAnonymous(proto), WaitForConstructorOf(proto)).ToArray();

            items.Select(x => new Positional(x.ID, x.Name, x.Age, x.Color, x.Converted)).Should().Equal(ExpectedPositional(Rows));
        }

        public record WithMoreParameters(int ID, int Missing, string AlsoMissing, int? MissingNullable);

        [Fact]
        public void ReadAllAnonymous_ParameterWithoutAField_GetsTheDefaultValueBeforeAndAfterTheThreshold()
        {
            var table = new ReaderTable().Column<int>("ID");
            for (int i = 0; i < Rows; i++)
            {
                table.Row(i);
            }

            var items = WaitingAtTheThreshold(table.CreateReader().ReadAllAnonymous(default(WithMoreParameters), new ReadOptions(FieldsSelector.Source)),
                WaitForConstructor<WithMoreParameters>).ToArray();

            items.Should().Equal(Enumerable.Range(0, Rows).Select(i => new WithMoreParameters(i, 0, null, null)));
        }

        public record CompiledRecord(int ID, string Name);

        [Fact]
        public void ReadAllAnonymous_MoreRowsThanTheThreshold_CompilesTheConstructor()
        {
            var table = new ReaderTable().Column<int>("ID").Column<string>("Name");
            for (int i = 0; i < Threshold + 1; i++)
            {
                table.Row(i, "x");
            }

            table.CreateReader().ReadAllAnonymous(default(CompiledRecord)).Take(Threshold - 1).Count();
            var before = Constructor<CompiledRecord>();
            table.CreateReader().ReadAllAnonymous(default(CompiledRecord)).Count();
            WaitForConstructor<CompiledRecord>();
            var after = Constructor<CompiledRecord>();

            IsCompiled(before).Should().BeFalse();
            IsCompiled(after).Should().BeTrue();
        }

        public class ThrowingConstructor
        {
            public ThrowingConstructor(int id, int value)
            {
                if (value < 0)
                {
                    throw new InvalidOperationException("Negative value " + value);
                }
            }
        }

        [Fact]
        [Trait("Fixed", "I28")]
        public void ReadAllAnonymous_ConstructorThrows_ThrowsTheConstructorsExceptionBeforeAndAfterTheThreshold()
        {
            Action before = () => ThrowingSetterTable(5).CreateReader().ReadAllAnonymous(default(ThrowingConstructor)).Count();
            Action after = () => WaitingAtTheThreshold(ThrowingSetterTable(Rows).CreateReader().ReadAllAnonymous(default(ThrowingConstructor)),
                WaitForConstructor<ThrowingConstructor>).Count();

            before.Should().Throw<InvalidOperationException>().WithMessage("Negative value -1");
            after.Should().Throw<InvalidOperationException>().WithMessage("Negative value -1");
        }

        #endregion

        #region Writing (the source of a bulk copy)

        public class Source : AwkwardBase
        {
            public int Field;
            public int ID { get; set; }
            public string Name { get; set; }
            public int? NullableInt { get; set; }
            public Color Color { get; set; }
            public Guid Key { get; set; }
            public object Any { get; set; }
            public new long Hidden { get; set; }
            public int PrivateGet { private get; set; }
        }

        private static Source CreateSource(int i)
        {
            return new Source()
            {
                Field = i + 1,
                ID = i,
                Name = i % 5 == 0 ? null : "Name " + i,
                NullableInt = i % 3 == 0 ? null : (int?)i,
                Color = (Color)(i % 3),
                Key = new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                Any = i % 2 == 0 ? (object)i : "text",
                Hidden = i + 2L,
                Virtual = i + 3,
                Inherited = i + 4,
                PrivateGet = i + 5
            };
        }

        private static List<object[]> ReadToEnd<T>(UniversalDataReader<T> reader)
        {
            var rows = new List<object[]>();

            using (reader)
            {
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount * 2];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i * 2] = reader.IsDBNull(i);
                        row[i * 2 + 1] = reader.GetValue(i);
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }

        [Fact]
        public void ToDataReader_MoreRowsThanTheThreshold_ReturnsEveryRowTheSameWay()
        {
            var items = Enumerable.Range(0, Rows).Select(CreateSource).ToArray();

            var reader = WaitingAtTheThreshold(items, WaitForGetters<Source>).ToDataReader();
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var rows = ReadToEnd(reader);

            names.Should().Equal("Field", "ID", "Name", "NullableInt", "Color", "Key", "Any", "Hidden", "PrivateGet", "Virtual", "Inherited");
            rows.Should().HaveCount(Rows);
            for (int i = 0; i < Rows; i++)
            {
                var x = items[i];
                rows[i].Should().Equal(new object[]
                {
                    false, x.Field, false, x.ID, x.Name == null, x.Name, x.NullableInt == null, x.NullableInt, false, x.Color, false, x.Key,
                    false, x.Any, false, x.Hidden, false, i + 5, false, x.Virtual, false, x.Inherited
                }, "row {0} is read the same way before and after the threshold", i);
            }
        }

        public struct StructSource
        {
            public int Field;
            public int ID { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void ToDataReader_Struct_MoreRowsThanTheThreshold_ReturnsEveryRowTheSameWay()
        {
            var items = Enumerable.Range(0, Rows).Select(i => new StructSource() { Field = i + 1, ID = i, Name = "Name " + i }).ToArray();

            var rows = ReadToEnd(WaitingAtTheThreshold(items, WaitForGetters<StructSource>).ToDataReader());

            rows.Select(x => (x[1], x[3], x[5])).Should().Equal(items.Select(x => ((object)x.Field, (object)x.ID, (object)x.Name)));
        }

        public class CompiledSource
        {
            public int Field;
            public int Property { get; set; }
        }

        [Fact]
        public void ToDataReader_MoreRowsThanTheThreshold_CompilesTheGetters()
        {
            var items = Enumerable.Range(0, Threshold + 1).Select(i => new CompiledSource() { Field = i, Property = i }).ToArray();

            ReadToEnd(items.Take(Threshold - 1).ToDataReader());
            var before = FieldSettings.FromType<CompiledSource>();
            ReadToEnd(items.ToDataReader());
            WaitForGetters<CompiledSource>();
            var after = FieldSettings.FromType<CompiledSource>();

            before.Select(x => IsCompiled(x.Getter)).Should().Equal(false, false);
            after.Select(x => IsCompiled(x.Getter)).Should().Equal(true, true);
            after.Select(x => (x.Name, x.FieldType, x.DataTypeName, x.DetermineNullByValue)).Should()
                .Equal(before.Select(x => (x.Name, x.FieldType, x.DataTypeName, x.DetermineNullByValue)));
        }

        // a reader that was created before the getters were compiled takes them over while it is read
        [Fact]
        public void ToDataReader_ReaderCreatedBeforeTheThreshold_ReturnsEveryRowTheSameWay()
        {
            var items = Enumerable.Range(0, Rows).Select(i => new CompiledSourceForTwoReaders() { ID = i, Name = "Name " + i }).ToArray();
            using var first = WaitingAtTheThreshold(items, WaitForGetters<CompiledSourceForTwoReaders>).ToDataReader();
            using var second = items.ToDataReader();
            var fromFirst = new List<(object, object)>();
            var fromSecond = new List<(object, object)>();

            // the readers take turns, so the second one is in the middle of its rows when the getters are compiled
            while (first.Read() && second.Read())
            {
                fromFirst.Add((first.GetValue(0), first.GetValue(1)));
                fromSecond.Add((second.GetValue(0), second.GetValue(1)));
            }

            var expected = items.Select(x => ((object)x.ID, (object)x.Name)).ToArray();
            fromFirst.Should().Equal(expected);
            fromSecond.Should().Equal(expected);
        }

        public class CompiledSourceForTwoReaders
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        public class ThrowingGetter
        {
            public int ID { get; set; }
            public int Value => ID < 0 ? throw new InvalidOperationException("Negative ID " + ID) : ID;
        }

        [Fact]
        [Trait("Fixed", "I28")]
        public void ToDataReader_GetterThrows_ThrowsTheGettersExceptionBeforeAndAfterTheThreshold()
        {
            var items = Enumerable.Range(0, Rows).Select(i => new ThrowingGetter() { ID = i == 5 || i == Rows - 1 ? -1 : i }).ToArray();
            using var reader = WaitingAtTheThreshold(items, WaitForGetters<ThrowingGetter>).ToDataReader();
            var failed = new List<int>();

            for (int i = 0; reader.Read(); i++)
            {
                Action act = () => reader.GetValue(1);

                if (i == 5 || i == Rows - 1)
                {
                    act.Should().Throw<InvalidOperationException>().WithMessage("Negative ID -1");
                    failed.Add(i);
                }
                else
                {
                    reader.GetValue(1).Should().Be(i);
                }
            }

            failed.Should().Equal(5, Rows - 1);
        }

        public class NullItem
        {
            public int Field;
            public int Property { get; set; }
        }

        // what reflection throws for a null object, kept by the compiled getters
        [Fact]
        [Trait("Quirk", "Q44")]
        public void ToDataReader_NullItem_ThrowsTargetExceptionBeforeAndAfterTheThreshold()
        {
            var items = Enumerable.Range(0, Rows).Select(i => i == 5 || i == Rows - 1 ? null : new NullItem() { Field = i, Property = i }).ToArray();
            using var reader = WaitingAtTheThreshold(items, WaitForGetters<NullItem>).ToDataReader();
            int failed = 0;

            for (int i = 0; reader.Read(); i++)
            {
                if (items[i] == null)
                {
                    Action field = () => reader.GetValue(0);
                    Action property = () => reader.GetValue(1);
                    Action isNull = () => reader.IsDBNull(1);

                    field.Should().Throw<TargetException>().WithMessage("Non-static field requires a target.");
                    property.Should().Throw<TargetException>().WithMessage("Non-static method requires a target.");
                    isNull.Should().Throw<TargetException>().WithMessage("Non-static method requires a target.");
                    failed++;
                }
            }

            failed.Should().Be(2);
        }

        #endregion

        #region What FastSql keeps for a type, read through reflection

        private static readonly Assembly library = typeof(ICommandCreator).Assembly;

        private static bool IsCompiled(Delegate accessor)
        {
            // a delegate compiled from an expression tree has a closure of the expression compiler as its target;
            // null is reflection
            return accessor?.Target?.GetType().FullName == "System.Runtime.CompilerServices.Closure";
        }

        private static Type TypeMembers<T>()
        {
            return library.GetType("Gerakul.FastSql.Common.TypeMembers`1", true).MakeGenericType(typeof(T));
        }

        private static Dictionary<string, Delegate> Setters<T>()
        {
            var members = TypeMembers<T>().GetMethod("ForReading").Invoke(null, new object[] { FromTypeOption.Default });
            var all = (Array)members.GetType().GetProperty("All").GetValue(members);

            return all.Cast<object>().ToDictionary(
                x => (string)x.GetType().GetProperty("Name").GetValue(x),
                x => (Delegate)x.GetType().GetField("Compiled").GetValue(x));
        }

        private static Delegate Constructor<T>()
        {
            var constructor = TypeMembers<T>().GetProperty("Constructor").GetValue(null);
            return (Delegate)constructor.GetType().GetField("Compiled").GetValue(constructor);
        }

        // The accessors of a type are compiled on the thread pool once it reaches the threshold, and reflection goes
        // on until they are there. This waits for them after the item that reaches the threshold, so the items after
        // it are the ones that use them
        private static IEnumerable<T> WaitingAtTheThreshold<T>(IEnumerable<T> items, Action waitForCompile)
        {
            int count = 0;

            foreach (var item in items)
            {
                yield return item;

                if (++count == Threshold)
                {
                    waitForCompile();
                }
            }
        }

        private static void WaitForSetters<T>()
        {
            WaitForCompile(TypeMembers<T>().GetMethod("ForReading").Invoke(null, new object[] { FromTypeOption.Default }));
        }

        private static void WaitForGetters<T>()
        {
            WaitForCompile(TypeMembers<T>().GetMethod("ForWriting").Invoke(null, new object[] { FromTypeOption.Default }));
        }

        private static void WaitForConstructor<T>()
        {
            WaitForCompile(TypeMembers<T>().GetProperty("Constructor").GetValue(null));
        }

        // for an anonymous type, which has no name to write WaitForConstructor<T> with
        private static Action WaitForConstructorOf<T>(T proto)
        {
            return WaitForConstructor<T>;
        }

        private static void WaitForCompile(object members)
        {
            var compiling = (Task)members.GetType().GetProperty("Compiling").GetValue(members);
            compiling.Should().NotBeNull("the threshold has been reached");
            compiling.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("the compile finishes");
        }

        #endregion
    }
}
