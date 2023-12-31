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

| Method                  | Mean        | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|-------:|----------:|
| BuildAndResolveExcos    | 19,673.2 ns | 352.05 ns | 312.08 ns | 4.3945 | 0.9766 |   27954 B |
| BuildAndResolveFM       |  6,090.5 ns | 118.09 ns | 157.65 ns | 2.8381 | 0.7019 |   17921 B |
| GetExcosSettings (Pool) |  1,486.3 ns |  23.47 ns |  19.60 ns | 0.2346 |      - |    1480 B |
| GetExcosSettings (New)  |  1,094.2 ns |  19.75 ns |  15.42 ns | 0.3147 |      - |    1976 B |
| GetFMSetting            |    382.4 ns |   5.06 ns |   4.73 ns | 0.1578 |      - |     992 B |
