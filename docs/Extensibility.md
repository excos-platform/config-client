# Extensibility

As I was writing the base code for the library I kept in mind it needs to be extensible in case I want to add something quickly in the future or in case it gets picked up by someone who has different needs than me.

You can integrate with different platforms and SDKs providing experimentation via the `IFeatureProvider` interface, while providing binding to options objects with `IConfigureOptions`.

You can create custom variant overrides (depending on the platform you run the code on) via the `IFeatureVariantOverride` interface.

You can create custom filters for features and variants with the `IFilteringCondition` and plug them into the configuration based provider with the `IFeatureFilterParser`.
