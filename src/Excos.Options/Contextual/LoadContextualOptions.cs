// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

internal class LoadContextualOptions<TOptions> : ILoadContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string? _name;
    private readonly string _configurationSection;
    private readonly IEnumerable<IFeatureProvider> _featureProviders;
    private readonly IEnumerable<IFeatureVariantOverride> _variantOverrides;
    private static readonly Lazy<string?> OptionsMetadataPropertyName = new(TryGetMetadataPropertyName, LazyThreadSafetyMode.PublicationOnly);

    public LoadContextualOptions(string? name, string configurationSection, IEnumerable<IFeatureProvider> featureProviders, IEnumerable<IFeatureVariantOverride> variantOverrides)
    {
        _name = name;
        _configurationSection = configurationSection;
        _featureProviders = featureProviders;
        _variantOverrides = variantOverrides;
    }

    public ValueTask<IConfigureContextualOptions<TOptions>> LoadAsync<TContext>(string name, in TContext context, CancellationToken cancellationToken) where TContext : IOptionsContext
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);

        if (_name == null || name == _name)
        {
            return LoadExperimentAsync(context, cancellationToken);
        }

        return new ValueTask<IConfigureContextualOptions<TOptions>>(NullConfigureContextualOptions.GetInstance<TOptions>());
    }

    private async ValueTask<IConfigureContextualOptions<TOptions>> LoadExperimentAsync<TContext>(TContext context, CancellationToken cancellationToken) where TContext : IOptionsContext
    {
        var configure = new ConfigureContextualOptions<TOptions>(_configurationSection);

        var receiver = new ContextReceiver();
        context.PopulateReceiver(receiver);

        // only instantiate metadata if expected by options type
        var optionsMetadataPropertyName = OptionsMetadataPropertyName.Value;
        FeatureMetadata? metadataCollection = optionsMetadataPropertyName != null ? new() : null;

        foreach (var provider in _featureProviders)
        {
            var features = await provider.GetFeaturesAsync(cancellationToken);

            var applicableFeatures = features
                .Where(e => e.Enabled)
                .Where(e => receiver.Satisfies(e.Filters));
            foreach (var feature in applicableFeatures)
            {
                var variantOverride = await TryGetVariantOverrideAsync(feature, context, cancellationToken);

                if (variantOverride != null)
                {
                    var (variant, metadata) = variantOverride.Value;
                    configure.ConfigureOptions.Add(variant.Configuration);
                    metadataCollection?.Features.Add(new()
                    {
                        FeatureName = feature.Name,
                        FeatureProvider = feature.ProviderName,
                        VariantId = variant.Id,
                        IsOverridden = true,
                        OverrideProviderName = metadata.OverrideProviderName,
                    });
                }
                else
                {
                    var allocationSpot = receiver.GetIdentifierAllocationSpot(feature.Salt);
                    var matchingVariant = feature.Variants
                        .Where(v => v.Allocation.Contains(allocationSpot))
                        .Where(v => receiver.Satisfies(v.Filters))
                        .OrderByDescending(v => v.Filters.Count) // the one with the most filters first
                        .OrderBy(v => v.Priority) // the one with lowest priority first (if specified) // TODO needs to be unit tested for null handling
                        .FirstOrDefault();

                    if (matchingVariant != null)
                    {
                        configure.ConfigureOptions.Add(matchingVariant.Configuration);
                        metadataCollection?.Features.Add(new()
                        {
                            FeatureName = feature.Name,
                            FeatureProvider = feature.ProviderName,
                            VariantId = matchingVariant.Id,
                        });
                    }
                }
            }
        }

        if (metadataCollection?.Features.Count > 0)
        {
            configure.ConfigureOptions.Add(new ConfigureFeatureMetadata(metadataCollection, optionsMetadataPropertyName!));
        }

        return configure;
    }

    private static string? TryGetMetadataPropertyName()
    {
        foreach (var property in typeof(TOptions).GetProperties())
        {
            if (property.PropertyType == typeof(FeatureMetadata))
            {
                return property.Name;
            }
        }

        return null;
    }

    private async Task<(Variant variant, VariantOverride metadata)?> TryGetVariantOverrideAsync<TContext>(Feature feature, TContext optionsContext, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        foreach (var @override in _variantOverrides)
        {
            var variantOverride = await @override.TryOverrideAsync(feature, optionsContext, cancellationToken);
            if (variantOverride != null && feature.Variants.TryGetValue(variantOverride.Id, out var selectedVariant))
            {
                return (selectedVariant, variantOverride);
            }
        }

        return null;
    }
}
