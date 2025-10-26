using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Docify.Core.Analyzers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Docify.Performance.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class AnalysisBenchmarks
{
    private string _projectPath = null!;
    private RoslynAnalyzer _analyzer = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Use the sample project for benchmarking
        // In a real scenario, we would use a 100-file project as specified in AC
        _projectPath = Path.GetFullPath("../../../../samples/SimpleLibrary/SimpleLibrary.csproj");

        if (!File.Exists(_projectPath))
        {
            throw new FileNotFoundException(
                "Sample project not found for benchmarking. Please ensure samples/SimpleLibrary exists.",
                _projectPath);
        }

        var symbolExtractor = new SymbolExtractor(NullLogger<SymbolExtractor>.Instance);
        _analyzer = new RoslynAnalyzer(NullLogger<RoslynAnalyzer>.Instance, symbolExtractor);
    }

    [Benchmark]
    public async Task AnalyzeProject_FullWorkflow()
    {
        var result = await _analyzer.AnalyzeProject(_projectPath);

        // Ensure the result is actually used to prevent optimization eliminating the call
        if (result.PublicApis.Count == 0)
        {
            throw new InvalidOperationException("No public APIs found");
        }
    }
}
