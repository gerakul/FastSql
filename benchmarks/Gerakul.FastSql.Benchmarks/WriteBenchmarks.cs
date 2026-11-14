using BenchmarkDotNet.Attributes;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.Benchmarks;

// Writing: the source of a bulk copy. SqlBulkCopy and the PostgreSQL copy read every cell with GetValue;
// SqlBulkCopy with EnableStreaming asks IsDBNull first
[MemoryDiagnoser]
public class WriteBenchmarks
{
    private Row[] rows = null!;

    [GlobalSetup]
    public void Setup()
    {
        rows = TestData.CreateRows(1000);
    }

    [Benchmark]
    public int FromType()
    {
        return FieldSettings.FromType<Row>().Length;
    }

    [Benchmark]
    public int ToDataReader_GetValue()
    {
        using var reader = rows.ToDataReader();
        int count = 0;

        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                reader.GetValue(i);
            }

            count++;
        }

        return count;
    }

    [Benchmark]
    public int ToDataReader_IsDBNullAndGetValue()
    {
        using var reader = rows.ToDataReader();
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
}
