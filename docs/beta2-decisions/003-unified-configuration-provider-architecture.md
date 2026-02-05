# Decision 003: Unified Configuration Provider Architecture

**Date**: 2026-02-02  
**Status**: Implemented ✅  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

The current architecture has several issues with configuration providers:

1. **GrowthBook-specific implementation** - `GrowthBookConfigurationProvider` duplicates logic that should be generic
2. **DI coupling** - Configuration providers cannot access DI services (built before service provider)
3. **Workaround patterns** - Mutable `GrowthBookConfigurationSource` shared between config and services
4. **Separate caching** - `GrowthBookFeatureProvider` is just a proxy for `GrowthBookFeatureCache`, adding indirection
5. **No standalone usage** - Cannot use configuration provider without full host builder setup

## Decision

### 1. Create Generic `ExcosConfigurationProvider`

A reusable configuration provider that works with any feature source:

```csharp
public class ExcosConfigurationProvider : ConfigurationProvider
{
    public ExcosConfigurationProvider(
        IEnumerable<IFeatureProvider> featureProviders,
        IOptionsContext context,
        TimeSpan? refreshPeriod = null);
}
```

**Key behaviors**:
- Takes multiple `IFeatureProvider` instances (supports composition)
- Uses provided `IOptionsContext` for variant filtering
- Converts matched variant configurations (`JsonElement`) to `IDictionary<string, string?>`
- Optional periodic refresh (if `refreshPeriod` is null, loads once)

### 2. Context Flexibility

Make it easy for consumers to provide context:

```csharp
// Option A: Custom IOptionsContext implementation
builder.AddExcosConfiguration(featureProviders, myCustomContext);

// Option B: Simple dictionary (wrapped internally)
builder.AddExcosConfiguration(featureProviders, new Dictionary<string, string> 
{ 
    ["Environment"] = "Production",
    ["Region"] = "US-West"
});
```

Implement `DictionaryOptionsContext` that wraps `IDictionary<string, string>` and implements `IOptionsContext`.

### 3. GrowthBook Configuration Extension

Standalone extension using options callback pattern:

```csharp
builder.Configuration.AddExcosGrowthBookConfiguration(options =>
{
    options.ApiHost = new Uri("https://cdn.growthbook.io");
    options.ClientKey = "sdk-xxx";
    options.Context = new Dictionary<string, string> { ["Market"] = "US" };
    options.RefreshPeriod = TimeSpan.FromMinutes(5);
    options.HttpMessageHandler = customHandler; // Optional
});
```

### 4. Accept Duplicate Caches When Used Separately

When configuration provider and contextual options are initialized independently:
- Each will have its own `GrowthBookFeatureProvider` instance
- Each will make its own API calls
- This is acceptable for simplicity

**However**, provide an `IHostBuilder` extension that shares a single provider:

```csharp
hostBuilder.ConfigureExcosWithGrowthBook(options => { ... });
// Uses same GrowthBookFeatureProvider for both config and contextual options
```

### 5. Consolidate GrowthBook Caching into Provider

**Current**: `GrowthBookFeatureProvider` → `GrowthBookFeatureCache` → `GrowthBookApiCaller`

**New**: `GrowthBookFeatureProvider` handles caching internally:
- Owns the HTTP client lifecycle
- Manages cache expiration
- Makes API calls directly
- Single class controls all data flow

This eliminates:
- `GrowthBookFeatureCache` (merged into provider)
- Indirection between provider and cache
- `BackgroundService` complexity for cache initialization

## Consequences

1. **Cleaner separation** - Configuration provider is generic, GrowthBook is just one feature source
2. **Flexible context** - Dictionary or custom `IOptionsContext` supported
3. **Standalone usage** - Can use `AddExcosGrowthBookConfiguration` without full DI setup
4. **Simpler GrowthBook internals** - One class (`GrowthBookFeatureProvider`) manages everything
5. **Explicit tradeoff** - Duplicate caches when used separately, shared when using host builder pattern

## Files Affected

### New Files
- `Excos.Options/Providers/ExcosConfigurationProvider.cs`
- `Excos.Options/Providers/ExcosConfigurationSource.cs`
- `Excos.Options/Providers/DictionaryOptionsContext.cs`

### Modified Files
- `Excos.Options.GrowthBook/GrowthBookFeatureProvider.cs` - Add caching logic
- `Excos.Options.GrowthBook/ServiceCollectionExtensions.cs` - Add new extension methods
- `Excos.Options.GrowthBook/GrowthBookOptions.cs` - Add Context, RefreshPeriod, HttpMessageHandler

### Deleted Files
- `Excos.Options.GrowthBook/GrowthBookFeatureCache.cs` - Merged into provider
- `Excos.Options.GrowthBook/GrowthBookConfigurationProvider.cs` - Replaced by generic provider
- `Excos.Options.GrowthBook/GrowthBookConfigurationSource.cs` - No longer needed

## Testing Strategy

1. **Unit tests for ExcosConfigurationProvider**:
   - Converts JsonElement variants to configuration dictionary
   - Respects variant priority ordering
   - Handles empty variant list
   - Periodic refresh triggers reload

2. **Integration tests for GrowthBook**:
   - Standalone configuration provider works without DI
   - Host builder extension shares single provider
   - Verify configuration values match expected from API response

3. **Context tests**:
   - Dictionary context filters variants correctly
   - Custom IOptionsContext implementations work
