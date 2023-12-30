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

| Method               | Mean         | Error        | StdDev       | Median       | Gen0   | Gen1   | Allocated |
|--------------------- |-------------:|-------------:|-------------:|-------------:|-------:|-------:|----------:|
| BuildAndResolveExcos | 28,111.09 ns | 1,989.788 ns | 5,866.935 ns | 24,148.72 ns | 4.4556 | 1.0986 |   27950 B |
| BuildAndResolveFM    |  9,208.25 ns |   699.455 ns | 2,040.344 ns |  9,061.90 ns | 2.5787 | 0.6409 |   16225 B |
| GetExcosSettings     |    966.71 ns |     9.294 ns |     7.761 ns |    963.99 ns | 0.3300 |      - |    2072 B |
| GetFMSetting         |     86.92 ns |     0.673 ns |     0.597 ns |     86.95 ns | 0.0331 |      - |     208 B |
