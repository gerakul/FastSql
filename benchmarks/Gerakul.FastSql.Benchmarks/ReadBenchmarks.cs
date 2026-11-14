using BenchmarkDotNet.Attributes;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.Benchmarks;

// Reading: one row shows what a query costs before its first row, 1,000 rows what a row costs
[MemoryDiagnoser]
public class ReadBenchmarks
{
    private static readonly RowRecord recordProto = null!; // ReadAllAnonymous takes the type from it, not the value

    private static readonly object anonymousProto = new
    {
        Id = 0,
        Name = "",
        Amount = 0m,
        Created = default(DateTime),
        Age = (int?)null,
        Active = false,
        Total = 0L,
        Key = default(Guid)
    };

    private object?[][] values = null!;

    [Params(1, 1000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        values = TestData.CreateValues(Rows);
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

        foreach (var item in TestData.CreateReader(values).ReadAll<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAll_Struct()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(values).ReadAll<RowStruct>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllAnonymous_Record()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(values).ReadAllAnonymous(recordProto))
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllAnonymous_AnonymousType()
    {
        return Count(TestData.CreateReader(values), anonymousProto);
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

    [Benchmark]
    public int ReadAll_Objects()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(values).ReadAll())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ReadAllFirstColumn_Int()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(values).ReadAllFirstColumn<int>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> ReadAllAsync_Class()
    {
        int count = 0;

        await foreach (var item in TestData.CreateReader(values).ReadAllAsync<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int ExecuteQuery_Class()
    {
        int count = 0;

        foreach (var item in new ArrayCommand(() => TestData.CreateReader(values)).ExecuteQuery<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> ExecuteQueryAsync_Class()
    {
        int count = 0;

        await foreach (var item in new ArrayCommand(() => TestData.CreateReader(values)).ExecuteQueryAsync<Row>())
        {
            count++;
        }

        return count;
    }
}
