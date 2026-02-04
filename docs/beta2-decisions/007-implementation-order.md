# Decision 007: Implementation Order and Phases

**Date**: 2026-02-02  
**Status**: Accepted  
**Context**: Refactoring for extensibility and clean architecture

## Implementation Phases

### Phase 1: Core Data Model Changes
1. Add `JsonElement Configuration` property to `Variant` (keeping `IConfigureOptions` temporarily for compilation)
2. Create `DictionaryOptionsContext` implementing `IOptionsContext`
3. Create utility for `JsonElement` → `IDictionary<string, string?>` conversion
4. Create utility for `IConfigurationSection` → `JsonElement` conversion

**Tests first**: Utility conversion tests, DictionaryOptionsContext tests

### Phase 2: Feature Evaluation Changes
5. Change `IFeatureEvaluation.EvaluateFeaturesAsync` return type to `ValueTask<IEnumerable<Variant>>`
6. Update `FeatureEvaluation` implementation
7. Update `LoadContextualOptions` to use new return type and bind from `JsonElement`

**Tests first**: Feature evaluation tests with new signature

### Phase 3: Configuration Provider
8. Create `ExcosConfigurationProvider` and `ExcosConfigurationSource`
9. Create extension methods on `IConfigurationBuilder`

**Tests first**: Configuration provider integration tests

### Phase 4: Update Existing Providers
10. Update `ConfigureExcosFeatures` to produce `JsonElement` configurations
11. Update fluent builder to accept JSON strings

**Tests first**: Updated builder API tests, configuration-based feature tests

### Phase 5: GrowthBook Refactoring
12. Create `SimpleHttpClientFactory` and `StaticOptionsMonitor`
13. Consolidate cache logic into `GrowthBookFeatureProvider`
14. Add default value variants with wrapping logic
15. Add `AddExcosGrowthBookConfiguration` extension
16. Update `ConfigureExcosWithGrowthBook` extensions

**Tests first**: GrowthBook standalone and DI integration tests

### Phase 6: Cleanup
17. Remove `IConfigureOptions` interface and implementations
18. Remove `GrowthBookFeatureCache`, `GrowthBookApiCaller`, `GrowthBookConfigurationProvider`
19. Update documentation (Usage.md, GrowthBookGuide.md, Extensibility.md, DataModel.md)

## Testing Strategy

- Write tests against public APIs at the beginning of each phase
- Tests may fail initially until implementation is complete
- Run tests at end of phase or when validation is needed
- Existing tests should continue to pass (updated as needed for API changes)

## Documentation Updates

Update existing documents with new code examples:
- `docs/Usage.md` - Fluent builder with JSON strings
- `docs/GrowthBookGuide.md` - New extension methods
- `docs/Extensibility.md` - Updated extension points
- `docs/DataModel.md` - JsonElement configuration model

No separate migration guide needed - breaking changes documented in decision files.
