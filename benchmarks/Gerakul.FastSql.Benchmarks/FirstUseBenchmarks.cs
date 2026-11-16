using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Microsoft.Data.SqlClient;

namespace Gerakul.FastSql.Benchmarks;

// The first use of a type: every launch is a new process, and one call is measured. The library itself is
// warmed up with another type before (the setup), so the numbers show what a further type costs, not what
// the first query of a process costs
[SimpleJob(RunStrategy.ColdStart, launchCount: 20, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
public class FirstUseBenchmarks
{
    // more rows than any threshold inside the library
    private const int WarmUpRows = 50000;

    private object?[][] oneRow = null!;
    private object?[][] manyRows = null!;
    private object?[][] moreRows = null!;
    private Row[] rows = null!;
    private Row[] moreObjects = null!;
    private ConnectionContext sqlContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        oneRow = TestData.CreateValues(1);
        manyRows = TestData.CreateValues(5000);
        moreRows = TestData.CreateValues(20000);
        rows = TestData.CreateRows(5000);
        moreObjects = TestData.CreateRows(20000);
        sqlContext = SqlContextProvider.DefaultInstance.CreateContext(new SqlConnection());

        // the same code paths with another type
        var warmUpValues = TestData.CreateValues(WarmUpRows);
        foreach (var item in TestData.CreateReader(warmUpValues).ReadAll<WarmUpRow>())
        {
        }

        var warmUpRows = warmUpValues.Select(x => new WarmUpRow() { Id = (int)x[0]!, Name = (string?)x[1] }).ToArray();
        ReadToEnd(warmUpRows.ToDataReader());
        sqlContext.CreateInsert("Employee", warmUpRows[0], "Id");
        sqlContext.CreateInsert("Employee", warmUpRows[0], "Id");

        // work the library does in the background for the warm-up type must not overlap the measured call
        Thread.Sleep(500);
    }

    [Benchmark]
    public int Read_OneRow()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(oneRow).ReadAll<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int Read_5000Rows()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(manyRows).ReadAll<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int Read_20000Rows()
    {
        int count = 0;

        foreach (var item in TestData.CreateReader(moreRows).ReadAll<Row>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public IWrappedCommand CreateInsert()
    {
        return sqlContext.CreateInsert("Employee", rows[0], "Id");
    }

    [Benchmark]
    public int ToDataReader_5000Rows()
    {
        return ReadToEnd(rows.ToDataReader());
    }

    [Benchmark]
    public int ToDataReader_20000Rows()
    {
        return ReadToEnd(moreObjects.ToDataReader());
    }

    private static int ReadToEnd<T>(UniversalDataReader<T> reader)
    {
        int count = 0;

        using (reader)
        {
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    reader.GetValue(i);
                }

                count++;
            }
        }

        return count;
    }
}
