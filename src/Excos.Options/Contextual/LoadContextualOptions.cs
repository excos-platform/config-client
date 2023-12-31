// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
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
        bool FeatureFilterPredicate(Feature f) => filteringReceiver.Satisfies(f.Filters);
        bool VariantFilterPredicate(Variant v) => filteringReceiver.Satisfies(v.Filters);

        // only instantiate metadata if expected by options type
        var optionsMetadataPropertyName = OptionsMetadataPropertyName.Value;
        FeatureMetadata? metadataCollection = optionsMetadataPropertyName != null ? new() : null;

        foreach (var provider in _featureProviders)
        {
            var features = await provider.GetFeaturesAsync(cancellationToken).ConfigureAwait(false);

            var applicableFeatures = features
                .Where(static f => f.Enabled)
                .Where(FeatureFilterPredicate);
            foreach (var feature in applicableFeatures)
            {
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
                    var allocationUnit = feature.AllocationUnit ?? _options.CurrentValue.DefaultAllocationUnit;
                    using var allocationReceiver = AllocationContextReceiver.Get(allocationUnit, feature.Salt);
                    context.PopulateReceiver(allocationReceiver);
                    var allocationSpot = allocationReceiver.GetIdentifierAllocationSpot();
                    using var allocationPredicate = AllocationPredicate.Get(allocationSpot);

                    var matchingVariant = feature.Variants
                        .Where(allocationPredicate.Invoke)
                        .Where(VariantFilterPredicate)
                        .OrderByDescending(static v => v.Filters.Count) // the one with the most filters first
                        .OrderBy(static v => v.Priority, PriorityComparer.Instance) // the one with lowest priority first (if specified)
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
            configure.ConfigureOptions.Add(ConfigureFeatureMetadata.Get(metadataCollection, optionsMetadataPropertyName!));
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
    private class PriorityComparer : IComparer<int?>
    {
        public static PriorityComparer Instance { get; } = new PriorityComparer();
        public int Compare(int? x, int? y)
        {
            if (x == y) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            return x.Value.CompareTo(y.Value);
        }
    }

    private sealed class AllocationPredicate : IDisposable
    {
        private double _allocationSpot;
        private AllocationPredicate(double allocationSpot) => _allocationSpot = allocationSpot;
        public bool Invoke(Variant v) => v.Allocation.Contains(_allocationSpot);
        public void Dispose() => Return(this);

        public static AllocationPredicate Get(double allocationSpot)
        {
            if (PrivateObjectPool<AllocationPredicate>.Instance.TryGet(out var instance) && instance != null)
            {
                instance._allocationSpot = allocationSpot;
            }
            else
            {
                instance = new AllocationPredicate(allocationSpot);
            }
            
            return instance;
        }
        public static void Return(AllocationPredicate instance) =>
            PrivateObjectPool<AllocationPredicate>.Instance.Return(instance);
    }
}
