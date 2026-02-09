# Benchmark

I wanted to see how does Excos compare performance wise to [Microsoft.FeatureManagement]().
It seems that Options.Contextual version is only a little bit slower than FM, while the pure Excos feature evaluation may be faster.

However, I want to highlight here that Excos evaluation speed will depend on how the configuration binding and filtering is executed. This benchmark covers only a very small piece of the puzzle and real world performance characteristics may differ.

I also need to note that caching layer is present in the Options.Contextual version.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7623/24H2/2024Update/HudsonValley)
12th Gen Intel Core i5-1235U 2.50GHz, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```

| Method                            | Mean        | Error     | StdDev      | Gen0    | Gen1   | Allocated |
|---------------------------------- |------------:|----------:|------------:|--------:|-------:|----------:|
| BuildAndResolveExcos              | 19,935.6 ns | 387.93 ns |   446.74 ns |  5.4932 | 0.3662 |   34829 B |
| BuildAndResolveFM                 | 48,043.6 ns | 929.68 ns | 1,447.40 ns | 11.2305 | 0.4883 |   70529 B |
| GetExcosSettingsContextual        |    715.1 ns |   9.04 ns |     8.46 ns |  0.2699 |      - |    1696 B |
| GetExcosSettingsFeatureEvaluation |    337.8 ns |   0.83 ns |     0.69 ns |  0.0892 |      - |     560 B |
| GetFMSetting                      |    763.5 ns |   2.77 ns |     2.46 ns |  0.2022 |      - |    1272 B |

## Previous notes (before beta)

It is about 2x slower at the moment. However, I need to point out that it operates differently. With Excos and Contextual Options you're actually applying configuration over an Options class which can contain many different settings, while with FM you're checking a single feature flag to be True or False.

I was able to optimize the amount of allocations with object pools. The biggest memory and perf hog was using LINQ.
Given how most of the time you will be making fewer calls to Excos than to FM it might be enough at this point.