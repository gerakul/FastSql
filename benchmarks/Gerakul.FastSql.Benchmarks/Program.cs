using BenchmarkDotNet.Running;

namespace Gerakul.FastSql.Benchmarks;

// All benchmarks:   dotnet run -c Release --project benchmarks/Gerakul.FastSql.Benchmarks -- --filter *
// One class:        ... -- --filter *ReadBenchmarks*
// The results are written to BenchmarkDotNet.Artifacts/results
public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
