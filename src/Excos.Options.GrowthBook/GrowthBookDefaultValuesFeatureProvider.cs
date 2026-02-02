// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Feature provider that provides all GrowthBook features.
/// Features are updated by GrowthBookFeatureCache.
/// The ExcosConfigurationProvider filters these to only default value variants.
/// </summary>
internal class GrowthBookDefaultValuesFeatureProvider : IFeatureProvider
{
    private IEnumerable<Feature> _features = Enumerable.Empty<Feature>();
    private readonly object _lock = new();

    /// <summary>
    /// Updates the features. Called by GrowthBookFeatureCache when features are loaded/updated.
    /// </summary>
    public void UpdateFeatures(IEnumerable<Feature> features)
    {
        lock (_lock)
        {
            _features = features.ToList(); // Materialize to avoid enumeration issues
        }
    }

    public ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        IEnumerable<Feature> features;
        lock (_lock)
        {
            features = _features;
        }

        return ValueTask.FromResult(features);
    }
}
