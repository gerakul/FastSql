using System.Data;
using System.Data.Common;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.Benchmarks;

// Checks that the benchmarks do the work they are named after: the rows they read have the values of the data
// (a null Age, a Guid, every member), the readers of the bulk copy source return them, and the commands have the
// text and the parameters they should. The benchmarks only count rows or return a command, so a mapping that fills
// nothing would otherwise look like a fast one. Runs in CI (dotnet run ... -- --validate); the timings stay local
public static class Validation
{
    private static readonly List<string> failures = [];

    public static int Run()
    {
        failures.Clear();

        foreach (int rows in new[] { 1, 1000 })
        {
            ValidateReading(rows);
        }

        ValidateWriting();
        ValidateCommands();
        ValidateFirstUse();

        Console.WriteLine(failures.Count == 0 ? "Validation passed" : $"Validation failed: {failures.Count} check(s)");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Check(string what, bool passed, string? detail = null)
    {
        Console.WriteLine($"{(passed ? "ok    " : "FAILED")} {what}{(passed || detail == null ? "" : ": " + detail)}");

        if (!passed)
        {
            failures.Add(what);
        }
    }

    // every member of a row, in the order of TestData.Names; null for a null value
    private static object?[] Values(Row x) => [x.Id, x.Name, x.Amount, x.Created, x.Age, x.Active, x.Total, x.Key];

    private static object?[] Values(RowStruct x) => [x.Id, x.Name, x.Amount, x.Created, x.Age, x.Active, x.Total, x.Key];

    private static object?[] Values(RowRecord x) => [x.Id, x.Name, x.Amount, x.Created, x.Age, x.Active, x.Total, x.Key];

    private static object?[] ValuesByName(object x)
    {
        return TestData.Names.Select(name => x.GetType().GetProperty(name)?.GetValue(x)).ToArray();
    }

    private static object?[] ValuesOfArray(object[] x)
    {
        return x.Select(value => value is DBNull ? null : value).ToArray();
    }

    private static void CheckRows<T>(string what, IEnumerable<T> items, Func<T, object?[]> values, Row[] expected)
    {
        var actual = items.Select(values).ToList();

        if (actual.Count != expected.Length)
        {
            Check(what, false, $"{actual.Count} rows instead of {expected.Length}");
            return;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            var want = Values(expected[i]);

            if (!actual[i].SequenceEqual(want))
            {
                Check(what, false, $"row {i}: [{string.Join(", ", actual[i])}] instead of [{string.Join(", ", want)}]");
                return;
            }
        }

        // the data has rows with a null Age (every third, the first included) and a Guid in every row
        Check(what, true);
    }

    private static void ValidateReading(int rows)
    {
        var benchmarks = new ReadBenchmarks() { Rows = rows };
        benchmarks.Setup();
        var expected = TestData.CreateRows(rows);
        string n = $"Read, {rows} row(s): ";

        CheckRows(n + "ReadAll_Class", benchmarks.QueryClass(), Values, expected);
        CheckRows(n + "ReadAll_Struct", benchmarks.QueryStruct(), Values, expected);
        CheckRows(n + "ReadAllAnonymous_Record", benchmarks.QueryRecord(), Values, expected);
        CheckRows(n + "ReadAllAnonymous_AnonymousType", benchmarks.QueryAnonymousType(), ValuesByName, expected);
        CheckRows(n + "ReadAll_Objects", benchmarks.QueryObjects(), ValuesOfArray, expected);
        CheckRows(n + "ReadAllAsync_Class", benchmarks.QueryClassAsync().ToBlockingEnumerable(), Values, expected);
        CheckRows(n + "ExecuteQuery_Class", benchmarks.QueryCommand(), Values, expected);
        CheckRows(n + "ExecuteQueryAsync_Class", benchmarks.QueryCommandAsync().ToBlockingEnumerable(), Values, expected);
        Check(n + "ReadAllFirstColumn_Int", benchmarks.QueryFirstColumn().SequenceEqual(expected.Select(x => x.Id)));

        // and the benchmarks count what the queries return
        var counts = new Dictionary<string, int>()
        {
            ["ReaderLoop"] = benchmarks.ReaderLoop(),
            ["ReadAll_Class"] = benchmarks.ReadAll_Class(),
            ["ReadAll_Struct"] = benchmarks.ReadAll_Struct(),
            ["ReadAllAnonymous_Record"] = benchmarks.ReadAllAnonymous_Record(),
            ["ReadAllAnonymous_AnonymousType"] = benchmarks.ReadAllAnonymous_AnonymousType(),
            ["ReadAll_Objects"] = benchmarks.ReadAll_Objects(),
            ["ReadAllFirstColumn_Int"] = benchmarks.ReadAllFirstColumn_Int(),
            ["ReadAllAsync_Class"] = benchmarks.ReadAllAsync_Class().GetAwaiter().GetResult(),
            ["ExecuteQuery_Class"] = benchmarks.ExecuteQuery_Class(),
            ["ExecuteQueryAsync_Class"] = benchmarks.ExecuteQueryAsync_Class().GetAwaiter().GetResult()
        };

        foreach (var (name, count) in counts)
        {
            Check($"{n}{name} counts {rows}", count == rows, $"{count}");
        }
    }

    private static void ValidateWriting()
    {
        var benchmarks = new WriteBenchmarks();
        benchmarks.Setup();
        var expected = TestData.CreateRows(1000);

        Check("Write: FromType gives the members of Row",
            FieldSettings.FromType<Row>().Select(x => x.Name).SequenceEqual(TestData.Names) && benchmarks.FromType() == TestData.Names.Length);

        using var reader = benchmarks.CreateDataReader();
        var rows = new List<object[]>();

        while (reader.Read())
        {
            var row = new object[reader.FieldCount];

            for (int i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
            }

            rows.Add(row);
        }

        Check("Write: the reader has the fields of Row", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).SequenceEqual(TestData.Names));
        CheckRows("Write: ToDataReader returns the values", rows, ValuesOfArray, expected);
        Check("Write: ToDataReader_GetValue counts 1000", benchmarks.ToDataReader_GetValue() == 1000);
        Check("Write: ToDataReader_IsDBNullAndGetValue counts 1000", benchmarks.ToDataReader_IsDBNullAndGetValue() == 1000);
    }

    // a creator that keeps the command it creates, on the context the benchmark uses
    private sealed class CommandCapture : ICommandCreator
    {
        private readonly ScopedContext context;

        public CommandCapture(ScopedContext context)
        {
            this.context = context;
        }

        public DbCommand? Command { get; private set; }

        public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
        {
            var command = commandGetter(context);
            Command = command;
            return context.Set(_ => command);
        }
    }

    private static string ParameterName(DbParameter parameter)
    {
        return parameter.ParameterName.TrimStart('@', ':');
    }

    // the parameters, by name without a prefix, with the values of the row's members of those names
    private static void CheckCommand(string what, DbCommand? command, CommandType type, string? text, Row row, params string[] names)
    {
        if (command == null)
        {
            Check(what, false, "no command was created");
            return;
        }

        var actual = command.Parameters.Cast<DbParameter>().ToDictionary(ParameterName, x => x.Value);
        var problems = new List<string>();

        if (command.CommandType != type)
        {
            problems.Add($"command type {command.CommandType}");
        }

        if (text != null && command.CommandText != text)
        {
            problems.Add($"text '{command.CommandText}'");
        }

        if (!actual.Keys.Order().SequenceEqual(names.Order()))
        {
            problems.Add($"parameters {string.Join(", ", actual.Keys)} instead of {string.Join(", ", names)}");
        }
        else
        {
            foreach (var name in names)
            {
                var want = typeof(Row).GetProperty(name)!.GetValue(row) ?? DBNull.Value;

                if (!Equals(actual[name], want))
                {
                    problems.Add($"{name} = {actual[name]} instead of {want}");
                }
            }
        }

        Check(what, problems.Count == 0, string.Join("; ", problems));
    }

    private static void ValidateCommands()
    {
        var benchmarks = new CommandBenchmarks();
        benchmarks.Setup();
        var captures = new List<CommandCapture>();
        benchmarks.UseCreators(context =>
        {
            var capture = new CommandCapture(context);
            captures.Add(capture);
            return capture;
        });
        var sql = captures[0];
        var npgsql = captures[1];
        var row = benchmarks.row;
        string[] all = TestData.Names;
        string[] withoutId = all.Skip(1).ToArray();
        string[] mapped = ["Id", "Age", "Name"];

        Check("Command: the row has a null Age", row.Age == null);

        // the floor and CreateInsert build the same command on the same connection
        var floor = benchmarks.AdoNet_Insert();
        CheckCommand("Command: AdoNet_Insert", floor, CommandType.Text, null, row, withoutId);

        benchmarks.CreateInsert();
        CheckCommand("Command: CreateInsert", sql.Command, CommandType.Text, floor.CommandText, row, withoutId);
        Check("Command: CreateInsert is on the connection of the floor", sql.Command?.Connection == floor.Connection && floor.Connection != null);

        benchmarks.CreateInsert_Precompiled();
        CheckCommand("Command: CreateInsert_Precompiled", sql.Command, CommandType.Text, floor.CommandText, row, withoutId);

        benchmarks.CreateUpdate();
        CheckCommand("Command: CreateUpdate", sql.Command, CommandType.Text, null, row, all);

        benchmarks.CreateDelete();
        CheckCommand("Command: CreateDelete", sql.Command, CommandType.Text, null, row, "Id");

        benchmarks.CreateMerge();
        CheckCommand("Command: CreateMerge", sql.Command, CommandType.Text, null, row, all);

        benchmarks.CreateMapped_Text();
        CheckCommand("Command: CreateMapped_Text", sql.Command, CommandType.Text, CommandBenchmarks.Text, row, mapped);

        benchmarks.CreateMapped_Text_AnonymousType();
        CheckCommand("Command: CreateMapped_Text_AnonymousType", sql.Command, CommandType.Text, CommandBenchmarks.Text, row, mapped);

        benchmarks.CreateMapped_Precompiled();
        CheckCommand("Command: CreateMapped_Precompiled", sql.Command, CommandType.Text, CommandBenchmarks.Text, row, mapped);

        benchmarks.CreateMapped_Text_PostgreSql();
        CheckCommand("Command: CreateMapped_Text_PostgreSql", npgsql.Command, CommandType.Text, null, row, mapped);

        benchmarks.CreateProcedure();
        CheckCommand("Command: CreateProcedure", sql.Command, CommandType.StoredProcedure, "SaveEmployee", row, all);

        benchmarks.CreateSimple();
        var simple = sql.Command?.Parameters.Cast<DbParameter>().Select(x => (ParameterName(x), x.Value)).ToArray();
        Check("Command: CreateSimple", simple != null && simple.SequenceEqual([("p0", (object?)1), ("p1", 40), ("p2", "A%")]),
            simple == null ? null : string.Join(", ", simple));

        Check("Command: CompileInsert", benchmarks.CompileInsert().CommandText == floor.CommandText);
        Check("Command: CompileMapped_Text", benchmarks.CompileMapped_Text().CommandText == CommandBenchmarks.Text);
    }

    private static void ValidateFirstUse()
    {
        var benchmarks = new FirstUseBenchmarks();
        benchmarks.Setup();

        Check("First use: Read_OneRow counts 1", benchmarks.Read_OneRow() == 1);
        Check("First use: Read_5000Rows counts 5000", benchmarks.Read_5000Rows() == 5000);
        Check("First use: Read_20000Rows counts 20000", benchmarks.Read_20000Rows() == 20000);
        Check("First use: ToDataReader_5000Rows counts 5000", benchmarks.ToDataReader_5000Rows() == 5000);
        Check("First use: ToDataReader_20000Rows counts 20000", benchmarks.ToDataReader_20000Rows() == 20000);
        Check("First use: CreateInsert creates a command", benchmarks.CreateInsert() != null);
    }
}
