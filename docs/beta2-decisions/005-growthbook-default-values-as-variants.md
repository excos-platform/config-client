# Decision 005: GrowthBook Default Values as Variants

**Date**: 2026-02-02  
**Status**: Accepted  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

GrowthBook features have a `defaultValue` property that represents the fallback configuration when no rules match. Currently this is handled separately:

- `GrowthBookFeatureParser.ConvertFeaturesToConfiguration()` extracts default values for the configuration provider
- `GrowthBookFeatureParser.ConvertFeaturesToExcos()` processes rules into variants for contextual options

This creates:
1. **Two code paths** - Different handling for defaults vs rules
2. **Inconsistent behavior** - Configuration provider only sees defaults, contextual sees rules
3. **Not compatible with generic provider** - `ExcosConfigurationProvider` expects all data through variants

## Decision

### Treat default values as variants

Create a "default" variant for each feature that represents the fallback configuration:

```csharp
new Variant
{
    Id = $"{featureName}:default",
    Filters = [],           // No filters = matches all contexts
    Priority = null,        // Null = lowest priority (per existing documentation)
    Configuration = ...     // Wrapped or unwrapped based on value kind
}
```

### Configuration wrapping rules

GrowthBook supports both structured JSON objects and primitive values as default/variation values:

1. **JSON Object (`JsonValueKind.Object`)** - Use as-is
   - Assumes the object follows Excos configuration structure
   - Example: `{ "MySection": { "Setting": "value" } }`

2. **Any other JSON kind (string, number, boolean, array, null)** - Wrap with feature name
   - Feature name becomes the configuration key
   - Example: `"current"` → `{ "checkout-layout": "current" }`

```csharp
JsonElement WrapConfiguration(string featureName, JsonElement value)
{
    if (value.ValueKind == JsonValueKind.Object)
    {
        return value; // Already structured
    }
    
    // Wrap primitive with feature name as key
    return JsonSerializer.SerializeToElement(
        new Dictionary<string, JsonElement> { [featureName] = value }
    );
}
```

### Priority semantics

Per existing `Variant` documentation, priority sorting uses:
- Lower numeric values = higher priority (applied last, wins)
- `null` = lowest priority (applied first, can be overridden)

Setting `Priority = null` for default variants ensures:
- Defaults are evaluated last in priority ordering
- Any rule-based variant with explicit priority will override
- Natural "fallback" behavior

### Example

GrowthBook feature:
```json
{
    "checkout-layout": {
        "defaultValue": "current",
        "rules": [
            {
                "condition": { "is_employee": true },
                "force": "dev"
            }
        ]
    }
}
```

Resulting variants:
```
Variant {
    Id = "checkout-layout:Force0",
    Filters = [EmployeeCondition],
    Priority = 0,
    Configuration = { "checkout-layout": "dev" }  // Wrapped
}

Variant {
    Id = "checkout-layout:default",
    Filters = [],
    Priority = null,                               // Lowest priority
    Configuration = { "checkout-layout": "current" }  // Wrapped
}
```

For an Excos-style JSON object:
```json
{
    "settings": {
        "defaultValue": {
            "MyOptions": {
                "Color": "Blue"
            }
        }
    }
}
```

Resulting default variant:
```
Variant {
    Id = "settings:default",
    Filters = [],
    Priority = null,
    Configuration = { "MyOptions": { "Color": "Blue" } }  // Used as-is
}
```

## Consequences

1. **Single code path** - All configuration flows through variants
2. **Consistent behavior** - Configuration provider and contextual options process the same data
3. **Natural override** - Rules override defaults via priority system
4. **Backward compatible** - External GrowthBook features with primitives still work

## Files Affected

### Modified Files
- `Excos.Options.GrowthBook/GrowthBookFeatureParser.cs` - Add default variant generation, add wrapping logic

### Deleted Code
- `GrowthBookFeatureParser.ConvertFeaturesToConfiguration()` - No longer needed (generic provider handles this)

## Testing Strategy

1. **Default variant creation**:
   - Default variant has `:default` suffix
   - Default variant has null priority
   - Default variant has empty filters

2. **Configuration wrapping**:
   - JSON objects passed through unchanged
   - Primitive strings wrapped with feature name
   - Primitive numbers wrapped with feature name
   - Primitive booleans wrapped with feature name
   - Arrays wrapped with feature name

3. **Priority ordering**:
   - Default variant sorted after all priority-specified variants
   - Multiple null-priority variants maintain stable order

4. **End-to-end**:
   - Configuration provider sees default values
   - Contextual options sees defaults overridden by matching rules
