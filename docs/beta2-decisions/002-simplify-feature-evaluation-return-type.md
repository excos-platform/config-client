# Decision 002: Simplify Feature Evaluation Return Type

**Date**: 2026-02-02  
**Status**: Implemented ✅  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

The current `IFeatureEvaluation.EvaluateFeaturesAsync` method returns `IAsyncEnumerable<Variant>`.

```csharp
IAsyncEnumerable<Variant> EvaluateFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken)
    where TContext : IOptionsContext;
```

This creates several issues:

1. **Unnecessary complexity** - Every consumer must use `await foreach` with `ConfigureAwait`
2. **No early termination benefit** - All consumers iterate through all variants
3. **Misleading abstraction** - Suggests streaming when data is loaded upfront from `IFeatureProvider.GetFeaturesAsync`
4. **Harder to work with** - Configuration providers need all variants at once for batch conversion

## Decision

**Change the return type to `ValueTask<IEnumerable<Variant>>`.**

```csharp
ValueTask<IEnumerable<Variant>> EvaluateFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken)
    where TContext : IOptionsContext;
```

### Design Choices

1. **`IEnumerable<Variant>` over `IReadOnlyList<Variant>`**:
   - Variants are processed in sequential order (later variants override earlier ones for same keys)
   - No random access is required
   - `IEnumerable` provides sufficient flexibility for ordered iteration
   - Implementation internally returns a materialized `List<Variant>`

2. **`ValueTask` over `Task`**:
   - Enables performance optimization when results can be returned synchronously (cached scenarios)
   - Avoids allocation of `Task` objects in hot paths
   - Standard pattern for high-performance async APIs

### Consequences

1. **Simpler consumption pattern**:
   ```csharp
   // Before
   await foreach (var variant in evaluation.EvaluateFeaturesAsync(context, ct).ConfigureAwait(false))
   {
       // process variant
   }
   
   // After
   var variants = await evaluation.EvaluateFeaturesAsync(context, ct).ConfigureAwait(false);
   foreach (var variant in variants)
   {
       // process variant
   }
   ```

2. **Clearer semantics** - Results are fully evaluated before consumption

3. **Easier integration with configuration provider** - Can convert all variants to configuration dictionary in one operation

## Files Affected

- `Excos.Options/IFeatureEvaluation.cs` - Update interface signature
- `Excos.Options/FeatureEvaluation.cs` - Update implementation to return `List<Variant>`
- `Excos.Options/Contextual/LoadContextualOptions.cs` - Update consumption pattern
- All test files using `EvaluateFeaturesAsync`

## Testing Strategy

1. Verify all existing tests compile and pass with new signature
2. Ensure variant ordering is preserved (priority-based sorting)
3. Test that empty results return empty enumerable (not null)
