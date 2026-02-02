# Migration Guide: v1.0-beta2

## Overview

Version 1.0-beta2 introduces a significant refactoring of the variant abstraction from `IConfigureOptions` (function-based) to `JsonElement` (data-based). This enables better introspection, batch processing, and new scenarios like dynamic configuration providers.

## Breaking Changes

### Options Provider API

The `Rollout` and `ABExperiment` methods now accept JSON strings instead of lambda functions.

**Before (beta1):**
```csharp
services.BuildFeature("MyFeature")
    .Rollout<MyOptions>(75, (options, section) => 
    {
        options.Value = "Hello";
        options.Count = 42;
    })
    .Save();
```

**After (beta2):**
```csharp
services.BuildFeature("MyFeature")
    .Rollout(75, """
    {
        "MySection": {
            "Value": "Hello",
            "Count": 42
        }
    }
    """)
    .Save();
```

**Note:** The JSON must include the section name as the root object key (e.g., `"MySection"`).

### A/B Experiments

**Before (beta1):**
```csharp
services.BuildFeature("ExperimentFeature")
    .ABExperiment<MyOptions>(
        configureA: (options, _) => options.Setting = "A",
        configureB: (options, _) => options.Setting = "B",
        allocationUnit: "UserId")
    .Save();
```

**After (beta2):**
```csharp
services.BuildFeature("ExperimentFeature")
    .ABExperiment(
        """{"MySection":{"Setting":"A"}}""",
        """{"MySection":{"Setting":"B"}}""",
        allocationUnit: "UserId")
    .Save();
```

## New Features

### VariantConfigurationUtilities

New public utility class for working with variant configurations:

```csharp
using Excos.Options;

// Convert JSON configurations to dictionary
var configurations = new[] { jsonElement1, jsonElement2 };
var dict = VariantConfigurationUtilities.ToConfigurationDictionary(configurations);

// Convert to IConfiguration
var config = VariantConfigurationUtilities.ToConfiguration(configurations);

// Create configure action for options binding
var configureAction = VariantConfigurationUtilities.ToConfigureAction<MyOptions>(
    configurations, 
    "MySection");

// Validate and parse JSON
var jsonElement = VariantConfigurationUtilities.ParseJsonConfiguration("""{"Value":"Test"}""");
```

### ExcosConfigurationProvider

New dynamic context-based configuration provider that periodically refreshes features:

```csharp
// Add to configuration builder
builder.Configuration.AddExcosDynamicContext(
    context: new Dictionary<string, string> 
    {
        ["Market"] = "US",
        ["Platform"] = "Web"
    },
    featureProvider: featureProvider,
    refreshPeriod: TimeSpan.FromMinutes(10) // Optional, defaults to 15 minutes
);
```

This provider:
- Filters variants based on the provided context dictionary
- Periodically refetches features (default: 15 minutes)
- Automatically updates configuration when features change
- Useful for applications that don't use Options.Contextual but still want dynamic feature-based configuration

## Migration Strategy

### Step 1: Update Dependencies

Update to version 1.0.0-beta2:
```xml
<PackageReference Include="Excos.Options" Version="1.0.0-beta2" />
```

### Step 2: Convert Lambda Functions to JSON

For each `Rollout` or `ABExperiment` call:

1. Identify the section name (from `ConfigureExcos<T>("SectionName")`)
2. Convert the lambda body to JSON format
3. Wrap in an object with the section name as the key

**Example:**
```csharp
// Before
.Rollout<WeatherOptions>(100, (opts, _) => 
{
    opts.TemperatureScale = "Fahrenheit";
    opts.ForecastDays = 7;
})

// After
.Rollout(100, """
{
    "Weather": {
        "TemperatureScale": "Fahrenheit",
        "ForecastDays": 7
    }
}
""")
```

### Step 3: Validate JSON

The new API validates JSON at runtime and throws `JsonException` for invalid JSON:

```csharp
try
{
    services.BuildFeature("MyFeature")
        .Rollout(75, invalidJsonString)
        .Save();
}
catch (JsonException ex)
{
    // Handle invalid JSON
}
```

### Step 4: Test Thoroughly

Run your test suite to ensure all features work correctly with the new JSON-based configuration.

## Advanced Scenarios

### Custom Feature Providers

If you've created custom feature providers that return `Variant` objects, update them to use `JsonElement` instead of `IConfigureOptions`:

**Before:**
```csharp
var variant = new Variant
{
    Id = "my-variant",
    Configuration = new MyCustomConfigureOptions(data)
};
```

**After:**
```csharp
var json = JsonDocument.Parse("""{"Section":{"Key":"Value"}}""");
var variant = new Variant
{
    Id = "my-variant",
    Configuration = json.RootElement.Clone()
};
json.Dispose();
```

### Introspection

The new `JsonElement`-based approach enables introspection of variant configurations:

```csharp
foreach (var variant in feature)
{
    // Inspect the configuration without executing it
    var config = variant.Configuration;
    if (config.TryGetProperty("MySection", out var section))
    {
        if (section.TryGetProperty("ImportantSetting", out var setting))
        {
            Console.WriteLine($"Variant {variant.Id} has setting: {setting}");
        }
    }
}
```

## Benefits of the New Approach

1. **Introspection**: Inspect variant configurations without executing them
2. **Serialization**: Serialize and deserialize feature configurations
3. **Performance**: Batch process and cache configurations
4. **Validation**: Validate JSON format at configuration time
5. **Tooling**: Better IDE support and potential for schema validation
6. **Flexibility**: Dynamic context-based configuration providers

## Support

If you encounter issues during migration, please:
1. Check this migration guide
2. Review the updated documentation in docs/Usage.md
3. Open an issue on GitHub with details about your scenario

## Rollback

If you need to rollback to the previous version:
```xml
<PackageReference Include="Excos.Options" Version="1.0.0-beta1" />
```
