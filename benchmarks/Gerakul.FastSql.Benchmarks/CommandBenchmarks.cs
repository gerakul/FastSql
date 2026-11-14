using BenchmarkDotNet.Attributes;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Gerakul.FastSql.Benchmarks;

// Command creation: everything up to the DbCommand with its parameters. Nothing is executed, so the
// connections are never opened
[MemoryDiagnoser]
public class CommandBenchmarks
{
    private const string Text = "select * from Employee where CompanyID = @Id and Age > @Age and Name like @Name";

    private readonly Row row = TestData.CreateRows(2)[1];

    private ConnectionContext sqlContext = null!;
    private ConnectionContext npgsqlContext = null!;
    private MappedCommand<Row> precompiledInsert = null!;
    private MappedCommand<Row> precompiledMapped = null!;

    [GlobalSetup]
    public void Setup()
    {
        sqlContext = SqlContextProvider.DefaultInstance.CreateContext(new SqlConnection());
        npgsqlContext = NpgsqlContextProvider.DefaultInstance.CreateContext(new NpgsqlConnection());
        precompiledInsert = sqlContext.CommandCompilator.CompileInsert<Row>("Employee", "Id");
        precompiledMapped = sqlContext.CommandCompilator.CompileMapped<Row>(Text);
    }

    // the floor: the same command written by hand
    [Benchmark(Baseline = true)]
    public int AdoNet_Insert()
    {
        var cmd = new SqlCommand(precompiledInsert.CommandText);
        cmd.Parameters.AddWithValue("Name", row.Name);
        cmd.Parameters.AddWithValue("Amount", row.Amount);
        cmd.Parameters.AddWithValue("Created", row.Created);
        cmd.Parameters.AddWithValue("Age", (object?)row.Age ?? DBNull.Value);
        cmd.Parameters.AddWithValue("Active", row.Active);
        cmd.Parameters.AddWithValue("Total", row.Total);
        cmd.Parameters.AddWithValue("Key", row.Key);
        return cmd.Parameters.Count;
    }

    [Benchmark]
    public IWrappedCommand CreateInsert()
    {
        return sqlContext.CreateInsert("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateInsert_Precompiled()
    {
        return sqlContext.CreateMapped(precompiledInsert, row);
    }

    [Benchmark]
    public IWrappedCommand CreateUpdate()
    {
        return sqlContext.CreateUpdate("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateDelete()
    {
        return sqlContext.CreateDelete("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateMerge()
    {
        return sqlContext.CreateMerge("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text()
    {
        return sqlContext.CreateMapped(Text, row);
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text_AnonymousType()
    {
        return sqlContext.CreateMapped(Text, new { row.Id, row.Age, row.Name });
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Precompiled()
    {
        return sqlContext.CreateMapped(precompiledMapped, row);
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text_PostgreSql()
    {
        return npgsqlContext.CreateMapped(Text, row);
    }

    [Benchmark]
    public IWrappedCommand CreateProcedure()
    {
        return sqlContext.CreateProcedure("SaveEmployee", row);
    }

    [Benchmark]
    public IWrappedCommand CreateSimple()
    {
        return sqlContext.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1 and Name like @p2", 1, 40, "A%");
    }

    [Benchmark]
    public MappedCommand<Row> CompileInsert()
    {
        return sqlContext.CommandCompilator.CompileInsert<Row>("Employee", "Id");
    }

    [Benchmark]
    public MappedCommand<Row> CompileMapped_Text()
    {
        return sqlContext.CommandCompilator.CompileMapped<Row>(Text);
    }
}
