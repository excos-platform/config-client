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

| Method               | Mean        | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------- |------------:|----------:|----------:|-------:|-------:|----------:|
| BuildAndResolveExcos | 19,362.9 ns | 333.24 ns | 311.72 ns | 4.3945 | 0.9766 |   27954 B |
| BuildAndResolveFM    |  5,952.6 ns | 114.63 ns | 127.41 ns | 2.8381 | 0.7019 |   17921 B |
| GetExcosSettings     |  1,197.7 ns |  17.13 ns |  16.02 ns | 0.3071 |      - |    1936 B |
| GetFMSetting         |    369.9 ns |   3.68 ns |   3.26 ns | 0.1578 |      - |     992 B |
