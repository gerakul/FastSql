using BenchmarkDotNet.Attributes;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Gerakul.FastSql.Benchmarks;

// Command creation: everything up to the DbCommand with its parameters. Nothing is executed, so the
// connections are never opened. Validation checks the commands the benchmarks create
[MemoryDiagnoser]
public class CommandBenchmarks
{
    internal const string Text = "select * from Employee where CompanyID = @Id and Age > @Age and Name like @Name";

    // a null Age, so the commands carry a DBNull parameter, as real rows often do
    internal readonly Row row = TestData.CreateRows(4)[3];

    private SqlConnection sqlConnection = null!;
    private ConnectionContext sqlContext = null!;
    private ConnectionContext npgsqlContext = null!;
    private MappedCommand<Row> precompiledInsert = null!;
    private MappedCommand<Row> precompiledMapped = null!;

    // what the commands are created on: the contexts, or for Validation creators that keep the command
    private ICommandCreator sql = null!;
    private ICommandCreator npgsql = null!;

    [GlobalSetup]
    public void Setup()
    {
        sqlConnection = new SqlConnection();
        sqlContext = SqlContextProvider.DefaultInstance.CreateContext(sqlConnection);
        npgsqlContext = NpgsqlContextProvider.DefaultInstance.CreateContext(new NpgsqlConnection());
        precompiledInsert = sqlContext.CommandCompilator.CompileInsert<Row>("Employee", "Id");
        precompiledMapped = sqlContext.CommandCompilator.CompileMapped<Row>(Text);
        sql = sqlContext;
        npgsql = npgsqlContext;
    }

    internal void UseCreators(Func<ScopedContext, ICommandCreator> creatorFor)
    {
        sql = creatorFor(sqlContext);
        npgsql = creatorFor(npgsqlContext);
    }

    // the floor: the command of CreateInsert written by hand, on the same connection
    [Benchmark(Baseline = true)]
    public SqlCommand AdoNet_Insert()
    {
        var cmd = new SqlCommand(precompiledInsert.CommandText, sqlConnection);
        cmd.Parameters.AddWithValue("Name", row.Name);
        cmd.Parameters.AddWithValue("Amount", row.Amount);
        cmd.Parameters.AddWithValue("Created", row.Created);
        cmd.Parameters.AddWithValue("Age", (object?)row.Age ?? DBNull.Value);
        cmd.Parameters.AddWithValue("Active", row.Active);
        cmd.Parameters.AddWithValue("Total", row.Total);
        cmd.Parameters.AddWithValue("Key", row.Key);
        return cmd;
    }

    [Benchmark]
    public IWrappedCommand CreateInsert()
    {
        return sql.CreateInsert("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateInsert_Precompiled()
    {
        return sql.CreateMapped(precompiledInsert, row);
    }

    [Benchmark]
    public IWrappedCommand CreateUpdate()
    {
        return sql.CreateUpdate("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateDelete()
    {
        return sql.CreateDelete("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateMerge()
    {
        return sql.CreateMerge("Employee", row, "Id");
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text()
    {
        return sql.CreateMapped(Text, row);
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text_AnonymousType()
    {
        return sql.CreateMapped(Text, new { row.Id, row.Age, row.Name });
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Precompiled()
    {
        return sql.CreateMapped(precompiledMapped, row);
    }

    [Benchmark]
    public IWrappedCommand CreateMapped_Text_PostgreSql()
    {
        return npgsql.CreateMapped(Text, row);
    }

    [Benchmark]
    public IWrappedCommand CreateProcedure()
    {
        return sql.CreateProcedure("SaveEmployee", row);
    }

    [Benchmark]
    public IWrappedCommand CreateSimple()
    {
        return sql.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1 and Name like @p2", 1, 40, "A%");
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
