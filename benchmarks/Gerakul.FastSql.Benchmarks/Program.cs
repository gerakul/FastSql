using BenchmarkDotNet.Running;

namespace Gerakul.FastSql.Benchmarks;

// All benchmarks:   dotnet run -c Release --project benchmarks/Gerakul.FastSql.Benchmarks -- --filter *
// One class:        ... -- --filter *ReadBenchmarks*
// Faster, with fewer iterations (FirstUseBenchmarks has a job of its own, run it without these):
//                   ... -- --filter *ReadBenchmarks* *WriteBenchmarks* *CommandBenchmarks* --warmupCount 5 --iterationCount 15
// The results are written to BenchmarkDotNet.Artifacts/results
public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
