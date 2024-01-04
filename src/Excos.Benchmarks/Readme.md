# Benchmark

I wanted to see how does Excos compare performance wise to [Microsoft.FeatureManagement]().
It is about 2x slower at the moment. However, I need to point out that it operates differently. With Excos and Contextual Options you're actually applying configuration over an Options class which can contain many different settings, while with FM you're checking a single feature flag to be True or False.

I was able to optimize the amount of allocations with object pools. The biggest memory and perf hog was using LINQ.
Given how most of the time you will be making fewer calls to Excos than to FM it might be enough at this point.

```
BenchmarkDotNet v0.13.11, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
12th Gen Intel Core i5-1235U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
```

| Method                 | Mean        | Error     | StdDev      | Gen0   | Gen1   | Allocated |
|----------------------- |------------:|----------:|------------:|-------:|-------:|----------:|
| BuildAndResolveExcos   | 21,844.2 ns | 427.07 ns | 1,063.56 ns | 4.3945 | 0.9766 |   27954 B |
| BuildAndResolveFM      |  7,297.5 ns | 160.23 ns |   464.84 ns | 2.8381 | 0.7019 |   17921 B |
| GetExcosSettingsPooled |    996.2 ns |  19.13 ns |    16.96 ns | 0.0591 |      - |     376 B |
| GetExcosSettingsNew    |    872.9 ns |  17.20 ns |    16.89 ns | 0.1345 |      - |     848 B |
| GetFMSetting           |    387.3 ns |   7.05 ns |     8.92 ns | 0.1578 |      - |     992 B |
