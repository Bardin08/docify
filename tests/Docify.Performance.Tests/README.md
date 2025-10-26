# Docify Performance Tests

This project contains performance benchmarks for Docify using BenchmarkDotNet.

## Running Benchmarks

Performance benchmarks **must** be run in Release configuration:

```bash
cd tests/Docify.Performance.Tests
dotnet run -c Release
```

## Performance Targets (Epic 1)

- **Time:** Analysis of 100-file project must complete in <10 seconds
- **Memory:** Memory usage must stay under 300MB during analysis

## Baseline Results

Baseline results from Epic 1 completion are stored in the `baselines/` directory. These serve as a reference point for detecting performance regressions in future development.

To update baselines after running benchmarks, save the BenchmarkDotNet results to `baselines/epic-1-baseline.md` with the current date.

## Interpreting Results

BenchmarkDotNet will output:
- **Mean:** Average execution time
- **Error:** Standard error of the mean
- **StdDev:** Standard deviation
- **Allocated:** Total memory allocated during execution

Pay special attention to:
- Mean execution time (should be well below 10s for current sample project)
- Allocated memory (provides insight into memory pressure)

## Sample Project

Currently, benchmarks use `samples/SimpleLibrary` as the test project. For full Epic 1 validation, a 100-file project should be created in `samples/ComplexSolution/`.

## CI/CD Integration

Future work: Add GitHub Actions workflow to run benchmarks on pull requests and detect regressions automatically.
