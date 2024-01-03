// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

internal class LoadContextualOptions<TOptions> : ILoadContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string? _name;
    private readonly string _configurationSection;
    private readonly IEnumerable<IFeatureProvider> _featureProviders;
    private readonly IEnumerable<IFeatureVariantOverride> _variantOverrides;
    private readonly IOptionsMonitor<ExcosOptions> _options;
    private static readonly Lazy<string?> OptionsMetadataPropertyName = new(TryGetMetadataPropertyName, LazyThreadSafetyMode.PublicationOnly);

    public LoadContextualOptions(
        string? name,
        string configurationSection,
        IEnumerable<IFeatureProvider> featureProviders,
        IEnumerable<IFeatureVariantOverride> variantOverrides,
        IOptionsMonitor<ExcosOptions> options)
    {
        _name = name;
        _configurationSection = configurationSection;
        _featureProviders = featureProviders;
        _variantOverrides = variantOverrides;
        _options = options;
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
        var configure = ConfigureContextualOptions<TOptions>.Get(_configurationSection);

        using var filteringReceiver = FilteringContextReceiver.Get();
        context.PopulateReceiver(filteringReceiver);

        // only instantiate metadata if expected by options type
        var optionsMetadataPropertyName = OptionsMetadataPropertyName.Value;
        FeatureMetadata? metadataCollection = optionsMetadataPropertyName != null ? new() : null;

        foreach (var provider in _featureProviders)
        {
            var features = await provider.GetFeaturesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var feature in features)
            {
                if (!feature.Enabled)
                {
                    continue;
                }

                if (!filteringReceiver.Satisfies(feature.Filters))
                {
                    continue;
                }

                var variantOverride = await TryGetVariantOverrideAsync(feature, context, cancellationToken).ConfigureAwait(false);

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
                    double allocationSpot = CalculateAllocationSpot(context, feature.AllocationUnit, feature.Salt, feature.AllocationHash);
                    Variant? matchingVariant = TryFindMatchingVariant(filteringReceiver, context, feature, allocationSpot);

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
            configure.ConfigureOptions.Add(ConfigureFeatureMetadata.Get(metadataCollection, optionsMetadataPropertyName!));
        }

        return configure;
    }

    private double CalculateAllocationSpot<TContext>(TContext context, string? allocationUnit, string salt, IAllocationHash allocationHash)
        where TContext : IOptionsContext
    {
        allocationUnit ??= _options.CurrentValue.DefaultAllocationUnit;
        using var allocationReceiver = AllocationContextReceiver.Get(allocationUnit, salt);
        context.PopulateReceiver(allocationReceiver);
        var allocationSpot = allocationReceiver.GetIdentifierAllocationSpot(allocationHash);
        return allocationSpot;
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

    private Variant? TryFindMatchingVariant<TContext>(FilteringContextReceiver filteringReceiver, TContext context, Feature feature, double allocationSpot)
         where TContext : IOptionsContext
    {
        var variants = new List<Variant>(feature.Variants);
        variants.Sort(FilterCountComparer.Instance); // the one with the most filters first
        variants.Sort(PriorityComparer.Instance); // the one with lowest priority first (if specified)

        foreach (var variant in variants)
        {
            if (!filteringReceiver.Satisfies(variant.Filters))
            {
                continue;
            }

            var localAllocationSpot = allocationSpot;
            if (variant.AllocationUnit != null)
            {
                localAllocationSpot = CalculateAllocationSpot(context, variant.AllocationUnit, feature.Salt, variant.AllocationHash);
            }

            if (variant.Allocation.Contains(localAllocationSpot))
            {
                return variant;
            }
        }

        return null;
    }

    private async Task<(Variant variant, VariantOverride metadata)?> TryGetVariantOverrideAsync<TContext>(Feature feature, TContext optionsContext, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        foreach (var @override in _variantOverrides)
        {
            var variantOverride = await @override.TryOverrideAsync(feature, optionsContext, cancellationToken).ConfigureAwait(false);
            if (variantOverride != null && feature.Variants.TryGetValue(variantOverride.Id, out var selectedVariant))
            {
                return (selectedVariant, variantOverride);
            }
        }

        return null;
    }

    /// <summary>
    /// Comparer for priority values where nulls are always greater than values so in ascending order will be considered last 
    /// </summary>
    private class PriorityComparer : IComparer<Variant>
    {
        public static PriorityComparer Instance { get; } = new PriorityComparer();
        public int Compare(Variant? x, Variant? y)
        {
            if (x?.Priority == y?.Priority) return 0;
            if (x?.Priority == null) return 1;
            if (y?.Priority == null) return -1;
            return x.Priority.Value.CompareTo(y.Priority.Value);
        }
    }

    /// <summary>
    /// Compares filter counts, more first
    /// </summary>
    private class FilterCountComparer : IComparer<Variant>
    {
        public static FilterCountComparer Instance { get; } = new FilterCountComparer();
        public int Compare(Variant? x, Variant? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return (-1) * x.Filters.Count.CompareTo(y.Filters.Count);
        }
    }
}
