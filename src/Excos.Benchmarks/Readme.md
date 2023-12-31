# Benchmark

I wanted to see how does Excos compare performance wise to [Microsoft.FeatureManagement]().
It is 1 magnitude slower at the moment. However, I need to point out that it operates differently. With Excos and Contextual Options you're actually applying configuration over an Options class which can contain many different settings, while with FM you're checking a single feature flag to be True or False.

I will look at optimizing the performance (especially allocation size) of Excos in the future, but given how most of the time you will be making fewer calls to it than to FM it might be manageable.

```
BenchmarkDotNet v0.13.11, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
12th Gen Intel Core i5-1235U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
```

| Method               | Mean         | Error      | StdDev     | Median       | Gen0   | Gen1   | Allocated |
|--------------------- |-------------:|-----------:|-----------:|-------------:|-------:|-------:|----------:|
| BuildAndResolveExcos | 20,059.73 ns | 393.568 ns | 511.750 ns | 19,982.28 ns | 4.3945 | 0.9766 |   27954 B |
| BuildAndResolveFM    |  5,212.15 ns | 100.536 ns | 111.746 ns |  5,214.44 ns | 2.5635 | 0.6104 |   16225 B |
| GetExcosSettings     |  1,036.08 ns |  19.383 ns |  44.923 ns |  1,019.20 ns | 0.2918 |      - |    1840 B |
| GetFMSetting         |     73.78 ns |   1.096 ns |   0.972 ns |     73.85 ns | 0.0331 |      - |     208 B |
