# Decision 001: Variant Configuration as JsonElement

**Date**: 2026-02-02  
**Status**: Implemented ✅  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

The current `Variant.Configuration` property is of type `IConfigureOptions`, which represents **behavior** (a function that applies settings to an options object). Implementations include:

- `CallbackConfigureOptions<T>` - wraps an `Action<T, string>` lambda
- `ConfigurationBasedConfigureOptions` - wraps an `IConfiguration` section  
- `JsonConfigureOptions` (GrowthBook) - parses JSON into configuration

This approach has several limitations:

1. **No introspection** - Cannot examine what settings a configuration will apply without executing it
2. **No serialization** - Cannot persist or transmit configuration data
3. **No comparison** - Cannot detect if two configurations are equivalent
4. **No batching** - Cannot merge or optimize configurations from multiple sources
5. **Tight coupling** - Each provider implements its own configuration strategy

## Decision

**Change `Variant.Configuration` from `IConfigureOptions` (behavior) to `JsonElement` (data).**

### Rationale

- `JsonElement` is a standard .NET type with excellent serialization support
- JSON is the native format for external systems (GrowthBook, configuration files)
- Enables future integrations beyond `IConfiguration` (direct deserialization, schema validation, etc.)
- Data can be inspected, compared, logged, and cached independently of application logic

### Consequences

1. **Breaking change to fluent builder API**:
   - Before: `.Rollout<T>(75, (opts, _) => opts.Value = "X")`
   - After: `.Rollout(75, """{"Section":{"Value":"X"}}""")`

2. **Remove `IConfigureOptions` interface entirely**:
   - Contextual options already have `IConfigureContextualOptions<T>` abstraction
   - Configuration provider works with `IDictionary<string, string?>` directly
   - No common abstraction needed across these different consumption patterns

3. **Application logic moves to consumption point**:
   - Contextual options: Convert `JsonElement` → bind to options object
   - Configuration provider: Convert `JsonElement` → `IDictionary<string, string?>`

## Files Affected

- `Excos.Options/Abstractions/IConfigureOptions.cs` - **Delete**
- `Excos.Options/Abstractions/Data/Variant.cs` - Change `Configuration` type
- `Excos.Options/Providers/OptionsFeatureBuilder.cs` - Update builder API
- `Excos.Options/Providers/Configuration/ConfigurationBasedConfigureOptions.cs` - **Delete**
- `Excos.Options/Contextual/ConfigureContextualOptions.cs` - Update to work with JsonElement
- `Excos.Options.GrowthBook/JsonConfigureOptions.cs` - **Delete** (no longer needed)

## Testing Strategy

1. Update existing tests to use JSON string input for builder API
2. Add tests for `JsonElement` → configuration dictionary conversion
3. Add tests for `JsonElement` → options binding
4. Verify GrowthBook integration tests still pass with new data model

## Implementation Summary

### Files Deleted
- `Excos.Options/Abstractions/IConfigureOptions.cs`
- `Excos.Options/Providers/Configuration/ConfigurationBasedConfigureOptions.cs`
- `Excos.Options/Contextual/IPooledConfigureOptions.cs`
- `Excos.Options.Tests/NullConfigureOptions.cs`

### Files Modified
- `Excos.Options/Abstractions/Data/Variant.cs` - `Configuration` changed from `IConfigureOptions` to `JsonElement`
- `Excos.Options/Providers/OptionsFeatureBuilder.cs` - `ABExperiment` and `Rollout` accept JSON strings
- `Excos.Options/Contextual/ConfigureContextualOptions.cs` - Works with `List<JsonElement>`
- `Excos.Options/Contextual/LoadContextualOptions.cs` - Uses `ConfigurationJsons` property
- `Excos.Options/FeatureEvaluation.cs` - Binds `JsonElement` via `ConfigurationBuilder`
- `Excos.Options/Providers/Configuration/FeatureConfigurationExtensions.cs` - Converts `IConfiguration` to `JsonElement`
- `Excos.Options.GrowthBook/GrowthBookFeatureParser.cs` - Uses `JsonElement` directly
- `Excos.Options.GrowthBook/JsonConfigureOptions.cs` - Refactored to `JsonConfigurationFileParser`

### New Files Created
- `Excos.Options/Utils/JsonElementConversion.cs` - Conversion utilities
- `Excos.Options/Contextual/DictionaryOptionsContext.cs` - Simple context implementation

### Test Results
- All 287 tests passing
- 26 new tests added for JsonElement behavior

## Migration Guide

### Fluent Builder API

**Before:**
```csharp
services.AddExcos(excos => excos
    .AddFeature("MyFeature", feature => feature
        .Rollout<MyOptions>(100, (opts, ctx) => opts.Label = "New Value")));
```

**After:**
```csharp
services.AddExcos(excos => excos
    .AddFeature("MyFeature", feature => feature
        .Rollout(100, """{"MySection":{"Label":"New Value"}}""")));
```

### Key Differences

1. **No generic type parameter** - The options type is inferred from configuration binding
2. **JSON string instead of lambda** - Configuration is now declarative data
3. **Must wrap in section** - JSON must include the configuration section name

### Utility Methods Available

```csharp
using Excos.Options.Utils;

// Convert JsonElement to configuration dictionary
var dict = jsonElement.ToConfigurationDictionary();

// Convert IConfiguration to JsonElement  
var element = configuration.ToJsonElement();

// Wrap value in object wrapper (for prefixed binding)
var wrapped = JsonElementConversion.WrapInObject("prefix", jsonElement);
```
