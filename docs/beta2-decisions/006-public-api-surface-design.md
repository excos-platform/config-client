# Decision 006: Public API Surface Design

**Date**: 2026-02-02  
**Status**: Accepted  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

With the architectural changes from previous decisions, we need to define a clear and consistent public API surface that:
- Maintains backward compatibility where possible
- Provides flexibility for different usage patterns
- Follows established .NET conventions

## Decision

### Excos.Options (Core Library)

#### Contextual Options Registration
```csharp
// Existing - unchanged
services.ConfigureExcos<TOptions>("SectionName");
```

#### Configuration-Based Feature Provider
```csharp
// Existing method signature - unchanged
services.ConfigureExcosFeatures("Features");
```

**Internal change**: Convert `IConfigurationSection` to `JsonElement` for variant configuration.

Since the primary use case is `appsettings.json`, users continue to write:
```json
{
    "Features": {
        "MyFeature": {
            "Variants": {
                "A": {
                    "Allocation": "50%",
                    "Settings": {
                        "MySection": {
                            "Color": "Blue"
                        }
                    }
                }
            }
        }
    }
}
```

The `Settings` section is converted from `IConfigurationSection` → `JsonElement` internally. This feels native to configuration-based usage while aligning with the `JsonElement` data model.

#### Fluent Builder API
```csharp
// Updated - now takes JSON strings instead of lambdas
services.BuildFeature("FeatureName")
    .Rollout(75, """{"MySection":{"Color":"Blue"}}""")
    .Save();

services.BuildFeature("Experiment")
    .ABExperiment(
        """{"MySection":{"Variant":"A"}}""",
        """{"MySection":{"Variant":"B"}}""")
    .Save();
```

#### Generic Configuration Provider
```csharp
// New extension on IConfigurationBuilder
builder.Configuration.AddExcosConfiguration(
    featureProviders,           // IEnumerable<IFeatureProvider>
    context,                    // IOptionsContext or IDictionary<string, string>
    refreshPeriod: null);       // Optional TimeSpan
```

### Excos.Options.GrowthBook

#### Standalone Configuration Provider
```csharp
// No DI required - uses callback pattern
builder.Configuration.AddExcosGrowthBookConfiguration(options =>
{
    options.ApiHost = new Uri("https://cdn.growthbook.io");
    options.ClientKey = "sdk-xxx";
    options.Context = new Dictionary<string, string> { ["Market"] = "US" };
    options.RefreshPeriod = TimeSpan.FromMinutes(5);
    options.HttpMessageHandler = customHandler;  // Optional
    options.HttpClientFactory = factory;         // Optional, takes precedence
});
```

Internally creates a `StaticOptionsMonitor<GrowthBookOptions>` that returns the configured value without actual change monitoring.

#### DI-Based Contextual Options
```csharp
// Uses IOptionsMonitor<GrowthBookOptions> from DI
services.ConfigureExcosWithGrowthBook();

// Configure options via standard pattern
services.AddOptions<GrowthBookOptions>().BindConfiguration("GrowthBook");
```

This approach uses DI-managed `IHttpClientFactory` and `ILogger` instances.

#### Combined Host Builder Pattern
```csharp
// Shared provider for both config and contextual options
hostBuilder.ConfigureExcosWithGrowthBook(options =>
{
    options.ClientKey = "sdk-xxx";
    options.Context = new Dictionary<string, string> { ["Market"] = "US" };
});
```

### Helper Types

#### StaticOptionsMonitor
```csharp
/// <summary>
/// An IOptionsMonitor implementation that returns a fixed value.
/// Used for standalone scenarios without DI change tracking.
/// </summary>
internal class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _value;
    
    public StaticOptionsMonitor(T value) => _value = value;
    
    public T CurrentValue => _value;
    public T Get(string? name) => _value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
```

#### DictionaryOptionsContext
```csharp
/// <summary>
/// An IOptionsContext implementation backed by a string dictionary.
/// </summary>
public class DictionaryOptionsContext : IOptionsContext
{
    private readonly IDictionary<string, string> _values;
    
    public DictionaryOptionsContext(IDictionary<string, string> values)
    {
        _values = values;
    }
    
    public void PopulateReceiver(IOptionsContextReceiver receiver)
    {
        foreach (var (key, value) in _values)
        {
            receiver.Receive(key, value);
        }
    }
}
```

## Consequences

1. **Backward compatible** - Existing `ConfigureExcos` and `ConfigureExcosFeatures` signatures unchanged
2. **Breaking change** - Fluent builder now requires JSON strings (documented in Decision 001)
3. **Flexible options pattern** - Both `IOptionsMonitor` and callback patterns supported for GrowthBook
4. **Native feel** - Configuration-based features continue to use `appsettings.json` structure
5. **Consistent internals** - All paths convert to `JsonElement` regardless of source

## Files Affected

### New Files
- `Excos.Options/Providers/DictionaryOptionsContext.cs`
- `Excos.Options/Providers/ExcosConfigurationBuilderExtensions.cs`
- `Excos.Options.GrowthBook/StaticOptionsMonitor.cs`
- `Excos.Options.GrowthBook/GrowthBookConfigurationBuilderExtensions.cs`

### Modified Files
- `Excos.Options/Providers/OptionsFeatureBuilder.cs` - JSON string input
- `Excos.Options/Providers/Configuration/FeatureConfigurationExtensions.cs` - Convert to JsonElement
- `Excos.Options.GrowthBook/ServiceCollectionExtensions.cs` - Add callback overload

## Testing Strategy

1. **API compatibility tests**:
   - Existing `ConfigureExcos` usage compiles and works
   - Existing `ConfigureExcosFeatures` usage compiles and works

2. **Builder API tests**:
   - JSON string parsing works correctly
   - Invalid JSON throws appropriate exception
   - Complex nested structures are preserved

3. **Options monitor tests**:
   - `StaticOptionsMonitor` returns configured value
   - DI-based `IOptionsMonitor` receives callback configuration
   - Changes to `IOptionsMonitor` are reflected (DI case only)

4. **Context tests**:
   - `DictionaryOptionsContext` populates receiver correctly
   - Custom `IOptionsContext` implementations work with generic provider
