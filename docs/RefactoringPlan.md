# Variant Configuration Refactoring Plan

## Overview

This document outlines the plan to refactor the variant abstraction from using `IConfigureOptions` (a configuration function) to using `JsonElement` (a data object). This change enables better introspection, batch optimization, and new scenarios while maintaining backward compatibility through utility methods.

## Current State Analysis

### Current Architecture

1. **Variant.cs** - Contains `IConfigureOptions Configuration` property
2. **IConfigureOptions** - Interface with `Configure<TOptions>(TOptions input, string section)` method
3. **Implementations**:
   - `CallbackConfigureOptions<T>` - Wraps user-provided lambda functions (used in Options provider)
   - `ConfigurationBasedConfigureOptions` - Wraps IConfiguration (used in Configuration provider)
   - `JsonConfigureOptions` - Wraps JsonElement and converts to IConfiguration (used in GrowthBook)

4. **Contextual Loader** (`LoadContextualOptions.cs`):
   - Evaluates features for a context
   - Collects `IConfigureOptions` from matching variants
   - Aggregates them in `ConfigureContextualOptions<TOptions>`

5. **Providers**:
   - **OptionsFeatureProvider** - Uses `CallbackConfigureOptions` with user lambda functions
   - **ConfigurationFeatureProvider** - Uses `ConfigurationBasedConfigureOptions` 
   - **GrowthBookFeatureProvider** - Uses `JsonConfigureOptions`

### Key Usage Patterns

From `OptionsBasedFeaturesTests.cs`:
```csharp
.Rollout<TestOptions>(75, (options, _) => options.Label = "XX")
.ABExperiment<TestOptions>((options, _) => options.Length = 5, (options, _) => options.Length = 10)
```

## Proposed Changes

### Phase 1: Core Data Model Changes

#### 1.1 Update Variant.cs
Change from:
```csharp
public required IConfigureOptions Configuration { get; set; }
```

To:
```csharp
public required JsonElement Configuration { get; set; }
```

**Breaking Change**: Yes - all code that creates Variants must be updated.

#### 1.2 Create VariantUtilities Class

Create new public utility class `Excos.Options.VariantUtilities` with static methods:

```csharp
public static class VariantUtilities
{
    // Convert variants to dictionary (for Configuration framework)
    public static IDictionary<string, string?> ToConfigurationDictionary(
        IEnumerable<Variant> variants, 
        string? sectionPrefix = null);
    
    // Convert variants to IConfiguration
    public static IConfiguration ToConfiguration(
        IEnumerable<Variant> variants,
        string? sectionPrefix = null);
    
    // Create a Configure action for binding to options
    public static Action<TOptions> ToConfigureAction<TOptions>(
        IEnumerable<Variant> variants,
        string section) 
        where TOptions : class;
}
```

**Implementation Details**:
- Use the existing `JsonConfigurationFileParser` from `JsonConfigureOptions.cs`
- For dictionary: collect all variants' JsonElements and parse into dictionary
- For IConfiguration: build from dictionary using `ConfigurationBuilder`
- For Configure action: create lambda that binds IConfiguration to options

### Phase 2: Update Existing Providers

#### 2.1 Options Provider (Breaking Change)

Update `OptionsFeatureBuilder.cs` methods:

**Current**:
```csharp
public static OptionsFeatureBuilder Rollout<TOptions>(
    this OptionsFeatureBuilder optionsFeatureBuilder, 
    double percentage, 
    Action<TOptions, string> configure, 
    string allocationUnit = "UserId")
```

**New**:
```csharp
public static OptionsFeatureBuilder Rollout(
    this OptionsFeatureBuilder optionsFeatureBuilder, 
    double percentage, 
    string configurationJson,  // <-- Changed parameter
    string allocationUnit = "UserId")
```

Same change for `ABExperiment`.

**Migration Path**: Users must convert their lambda functions to JSON strings.

#### 2.2 Configuration Provider

Update `ConfigurationBasedConfigureOptions.cs` or its usage to create JsonElement directly from IConfiguration instead of wrapping in `IConfigureOptions`.

#### 2.3 GrowthBook Provider

Already uses JsonElement internally - simplify by removing `JsonConfigureOptions` wrapper and directly assigning JsonElement to Variant.Configuration.

### Phase 3: Contextual Loader Integration

Update `LoadContextualOptions.cs` to use `VariantUtilities`:

```csharp
private async ValueTask<IConfigureContextualOptions<TOptions>> GetConfigurationForFeaturesAsync<TContext>(
    TContext context, 
    CancellationToken cancellationToken)
    where TContext : IOptionsContext
{
    var variants = new List<Variant>();
    await foreach (var variant in _featureEvaluation.EvaluateFeaturesAsync(context, cancellationToken))
    {
        variants.Add(variant);
    }
    
    // Use utility to create configure action
    var configureAction = VariantUtilities.ToConfigureAction<TOptions>(variants, _configurationSection);
    
    return new ConfigureContextualOptions<TOptions>(configureAction);
}
```

### Phase 4: New Dynamic Configuration Provider

Create `Excos.Options.Providers.DynamicContextConfigurationProvider`:

```csharp
public class DynamicContextConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IDictionary<string, string> _context;
    private readonly IFeatureProvider _featureProvider;
    private readonly Timer _refreshTimer;
    private readonly TimeSpan _refreshPeriod;
    
    public DynamicContextConfigurationProvider(
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        _context = context;
        _featureProvider = featureProvider;
        _refreshPeriod = refreshPeriod ?? TimeSpan.FromMinutes(15);
        _refreshTimer = new Timer(OnRefreshTimer, null, _refreshPeriod, _refreshPeriod);
        
        // Initial load
        _ = RefreshAsync();
    }
    
    private async void OnRefreshTimer(object? state)
    {
        await RefreshAsync();
    }
    
    private async Task RefreshAsync()
    {
        // Get all features
        var features = await _featureProvider.GetFeaturesAsync(CancellationToken.None);
        
        // Filter variants based on context
        var matchedVariants = FilterVariantsByContext(features, _context);
        
        // Convert to dictionary
        var data = VariantUtilities.ToConfigurationDictionary(matchedVariants);
        
        // Update data and trigger reload
        Data = new Dictionary<string, string?>(data);
        OnReload();
    }
    
    private IEnumerable<Variant> FilterVariantsByContext(
        IEnumerable<Feature> features, 
        IDictionary<string, string> context)
    {
        // Create a simple context object from dictionary
        var dynamicContext = new DynamicContext(context);
        
        // Evaluate each feature's variants against context
        foreach (var feature in features)
        {
            foreach (var variant in feature)
            {
                if (AllFiltersMatch(variant, dynamicContext))
                {
                    yield return variant;
                    break; // Only one variant per feature
                }
            }
        }
    }
    
    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
```

**DynamicContext Implementation**:
```csharp
internal class DynamicContext : IOptionsContext
{
    private readonly IDictionary<string, string> _values;
    
    public DynamicContext(IDictionary<string, string> values)
    {
        _values = values;
    }
    
    public bool TryGetValue(string key, out object? value)
    {
        if (_values.TryGetValue(key, out var stringValue))
        {
            value = stringValue;
            return true;
        }
        value = null;
        return false;
    }
}
```

**Configuration Extension**:
```csharp
public static class DynamicContextConfigurationExtensions
{
    public static IConfigurationBuilder AddExcosDynamicContext(
        this IConfigurationBuilder builder,
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        return builder.Add(new DynamicContextConfigurationSource(
            context, 
            featureProvider, 
            refreshPeriod));
    }
}
```

## Testing Strategy

### New Tests to Add

All tests in `Excos.Options.Tests` (public API tests):

1. **VariantUtilitiesTests.cs**
   - `ToConfigurationDictionary_WithSingleVariant_ReturnsCorrectDictionary`
   - `ToConfigurationDictionary_WithMultipleVariants_MergesCorrectly`
   - `ToConfiguration_WithVariants_CreatesWorkingConfiguration`
   - `ToConfigureAction_WithVariants_BindsOptionsCorrectly`

2. **DynamicContextConfigurationProviderTests.cs**
   - `Constructor_InitializesWithContext`
   - `RefreshTimer_PeriodicallRefetches`
   - `ContextFiltering_MatchesCorrectVariants`
   - `OnReload_TriggersConfigurationReload`
   - `CustomRefreshPeriod_UsesProvidedValue`
   - `DefaultRefreshPeriod_Is15Minutes`

### Updated Tests

1. **OptionsBasedFeaturesTests.cs** - Update all tests to use JSON strings instead of lambda functions
2. **ConfigurationBasedFeaturesTest.cs** - May need updates depending on implementation
3. **GrowthBook tests** - Should mostly continue working as JsonElement is already used internally

## Migration Guide for Users

### Before (Lambda-based):
```csharp
services.BuildFeature("TestFeature")
    .Rollout<TestOptions>(75, (options, _) => options.Label = "XX")
    .Save();
```

### After (JSON-based):
```csharp
services.BuildFeature("TestFeature")
    .Rollout(75, """{"Label": "XX"}""")
    .Save();
```

### Using Utilities:
```csharp
// Get variants from evaluation
var variants = featureEvaluation.EvaluateFeaturesAsync(context).ToEnumerable();

// Convert to different formats
var dict = VariantUtilities.ToConfigurationDictionary(variants);
var config = VariantUtilities.ToConfiguration(variants);
var configure = VariantUtilities.ToConfigureAction<MyOptions>(variants, "MySection");
```

## Implementation Order

1. ✅ **Analysis Complete** - Understand current codebase
2. ⏳ **Plan Document** - This document
3. **Core Changes**:
   - Create `VariantUtilities` class with tests
   - Update `Variant.cs` to use `JsonElement`
   - Update `LoadContextualOptions.cs` to use utilities
4. **Provider Updates**:
   - Update GrowthBook provider (simplify)
   - Update Configuration provider
   - Update Options provider (breaking change)
5. **New Provider**:
   - Create `DynamicContextConfigurationProvider`
   - Add tests
   - Add documentation
6. **Test Updates**:
   - Update all existing tests
   - Run full test suite
7. **Documentation**:
   - Update Usage.md
   - Create migration guide
   - Update API docs

## Open Questions

1. **DynamicContext Implementation**: 
   - Current filtering logic in `LoadContextualOptions` uses strongly-typed context via `IOptionsContext`
   - Need to verify `IFilteringCondition` implementations can work with string-only dictionary context
   - **Options**:
     - A) Create adapter that implements `IOptionsContext` with dictionary backing (proposed above)
     - B) Add overloads to filtering conditions to accept dictionary directly
     - C) Use reflection to create dynamic type with properties from dictionary
   
   **Question for user**: Which approach do you prefer for the dynamic context?

2. **JSON String Format for Options Provider**:
   - Should we support both raw JSON string and pre-parsed JsonElement?
   - Should we add validation/schema for the JSON?
   
   **Question for user**: Any preference on JSON input validation?

3. **Backward Compatibility**:
   - This is a breaking change for the Options provider API
   - Should we provide any compatibility shim or version bump guidance?
   
   **Question for user**: How should we handle the version bump and breaking change communication?

4. **VariantUtilities Location**:
   - Should this be in `Excos.Options` namespace or a sub-namespace like `Excos.Options.Utilities`?
   
   **Question for user**: Namespace preference?

## Risk Assessment

### High Risk
- Breaking change to `Rollout` and `ABExperiment` APIs
- Changes to core Variant model affect all providers

### Medium Risk  
- Context filtering with dictionary-based context (needs careful implementation)
- Timer-based refresh in configuration provider (resource management)

### Low Risk
- VariantUtilities implementation (straightforward conversion)
- GrowthBook simplification (already uses JsonElement)

## Benefits

1. **Introspection**: Can inspect variant configurations without executing functions
2. **Performance**: Can batch-process variants, cache parsed configurations
3. **Serialization**: Can serialize/deserialize feature configurations
4. **Tooling**: Can build validation, IDE support, schema validation
5. **Flexibility**: Dynamic configuration provider enables runtime context evaluation
