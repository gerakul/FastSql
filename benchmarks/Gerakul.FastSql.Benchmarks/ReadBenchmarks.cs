using BenchmarkDotNet.Attributes;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.Benchmarks;

// Reading: one row shows what a query costs before its first row, 1,000 rows what a row costs. The library counts
// the rows of a type over the whole process, not per call, and the warm-up of a steady-state run reads far more
// rows than any such threshold: these numbers are the mapping of a type that is in use. The first rows of a type
// in a new process are what FirstUseBenchmarks measures.
//
// Each benchmark counts the rows of a query that a method of its own creates; Validation reads the rows of the
// same methods and checks their values, so a mapping that fills nothing cannot pass for a fast one
[MemoryDiagnoser]
public class ReadBenchmarks
{
    private static readonly RowRecord recordProto = null!; // ReadAllAnonymous takes the type from it, not the value

    private object?[][] values = null!;

    // built once in the setup, so that creating the prototype is not part of what is measured; one generic call
    // makes both, so they read the same anonymous type
    private Func<ArrayDataReader, int> countAnonymous = null!;
    private Func<ArrayDataReader, IEnumerable<object>> readAnonymous = null!;

    [Params(1, 1000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        values = TestData.CreateValues(Rows);
        (countAnonymous, readAnonymous) = ForAnonymousType(new
        {
            Id = 0,
            Name = "",
            Amount = 0m,
            Created = default(DateTime),
            Age = (int?)null,
            Active = false,
            Total = 0L,
            Key = default(Guid)
        });
    }

    // the floor: every value is read, nothing is mapped
    [Benchmark(Baseline = true)]
    public int ReaderLoop()
    {
        var reader = TestData.CreateReader(values);
        int count = 0;

        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    reader.GetValue(i);
                }
            }

            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAll_Class()
    {
        int count = 0;

        foreach (var item in QueryClass())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAll_Struct()
    {
        int count = 0;

        foreach (var item in QueryStruct())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllAnonymous_Record()
    {
        int count = 0;

        foreach (var item in QueryRecord())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllAnonymous_AnonymousType()
    {
        return countAnonymous(TestData.CreateReader(values));
    }

    [Benchmark]
    public int ReadAll_Objects()
    {
        int count = 0;

        foreach (var item in QueryObjects())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllFirstColumn_Int()
    {
        int count = 0;

        foreach (var item in QueryFirstColumn())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> ReadAllAsync_Class()
    {
        int count = 0;

        await foreach (var item in QueryClassAsync())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ExecuteQuery_Class()
    {
        int count = 0;

        foreach (var item in QueryCommand())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> ExecuteQueryAsync_Class()
    {
        int count = 0;

        await foreach (var item in QueryCommandAsync())
        {
            count++;
        }

        return count;
    }

    #region The queries, for the benchmarks and for Validation

    internal IEnumerable<Row> QueryClass()
    {
        return TestData.CreateReader(values).ReadAll<Row>();
    }

    internal IEnumerable<RowStruct> QueryStruct()
    {
        return TestData.CreateReader(values).ReadAll<RowStruct>();
    }

    internal IEnumerable<RowRecord> QueryRecord()
    {
        return TestData.CreateReader(values).ReadAllAnonymous(recordProto);
    }

    internal IEnumerable<object> QueryAnonymousType()
    {
        return readAnonymous(TestData.CreateReader(values));
    }

    internal IEnumerable<object[]> QueryObjects()
    {
        return TestData.CreateReader(values).ReadAll();
    }

    internal IEnumerable<int> QueryFirstColumn()
    {
        return TestData.CreateReader(values).ReadAllFirstColumn<int>();
    }

    internal IAsyncEnumerable<Row> QueryClassAsync()
    {
        return TestData.CreateReader(values).ReadAllAsync<Row>();
    }

    internal IEnumerable<Row> QueryCommand()
    {
        return new ArrayCommand(() => TestData.CreateReader(values)).ExecuteQuery<Row>();
    }

    internal IAsyncEnumerable<Row> QueryCommandAsync()
    {
        return new ArrayCommand(() => TestData.CreateReader(values)).ExecuteQueryAsync<Row>();
    }

    #endregion

    // the counting and the reading of one anonymous type: ReadAllAnonymous is called with it, not with object
    private static (Func<ArrayDataReader, int> Count, Func<ArrayDataReader, IEnumerable<object>> Read) ForAnonymousType<T>(T proto)
        where T : class
    {
        return (reader => Count(reader, proto), reader => reader.ReadAllAnonymous(proto));
    }

    private static int Count<T>(ArrayDataReader reader, T proto)
    {
        int count = 0;

        foreach (var item in reader.ReadAllAnonymous(proto))
        {
            count++;
        }

        return count;
    }
}
