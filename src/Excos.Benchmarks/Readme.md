# Benchmark

I wanted to see how does Excos compare performance wise to [Microsoft.FeatureManagement]().
It seems that Options.Contextual version is only a little bit slower than FM, while the pure Excos feature evaluation may be faster.

However, I want to highlight here that Excos evaluation speed will depend on how the configuration binding and filtering is executed. This benchmark covers only a very small piece of the puzzle and real world performance characteristics may differ.

```
BenchmarkDotNet v0.13.11, Windows 11 (10.0.22631.4602/23H2/2023Update/SunValley3)
12th Gen Intel Core i5-1235U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 8.0.401
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
```

| Method                            | Mean        | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------------------------------- |------------:|----------:|----------:|-------:|-------:|----------:|
| BuildAndResolveExcos              | 14,003.7 ns | 129.04 ns | 114.39 ns | 3.6011 | 0.8545 |   22784 B |
| BuildAndResolveFM                 |  5,979.1 ns |  92.25 ns |  86.29 ns | 2.8381 | 0.7019 |   17841 B |
| GetExcosSettingsContextual        |    439.9 ns |   8.43 ns |  10.35 ns | 0.1249 |      - |     784 B |
| GetExcosSettingsFeatureEvaluation |    287.3 ns |   3.59 ns |   3.36 ns | 0.0930 |      - |     584 B |
| GetFMSetting                      |    402.6 ns |   5.01 ns |   4.19 ns | 0.1578 |      - |     992 B |

## Previous notes (before beta)

It is about 2x slower at the moment. However, I need to point out that it operates differently. With Excos and Contextual Options you're actually applying configuration over an Options class which can contain many different settings, while with FM you're checking a single feature flag to be True or False.

I was able to optimize the amount of allocations with object pools. The biggest memory and perf hog was using LINQ.
Given how most of the time you will be making fewer calls to Excos than to FM it might be enough at this point.