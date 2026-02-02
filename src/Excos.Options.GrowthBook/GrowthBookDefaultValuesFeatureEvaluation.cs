// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Feature evaluation that provides default value variants from GrowthBook features.
/// Features are provided by GrowthBookFeatureCache.
/// </summary>
internal class GrowthBookDefaultValuesFeatureEvaluation : IFeatureEvaluation
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

    public ValueTask<IEnumerable<Variant>> EvaluateFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken) where TContext : IOptionsContext
    {
        IEnumerable<Feature> features;
        lock (_lock)
        {
            features = _features;
        }

        var defaultVariants = new List<Variant>();

        foreach (var feature in features)
        {
            // Get only the default variant (the one with int.MaxValue priority and no filters)
            var defaultVariant = feature.FirstOrDefault(v => v.Id.EndsWith(GrowthBookConstants.DefaultVariantSuffix));
            if (defaultVariant != null)
            {
                defaultVariants.Add(defaultVariant);
            }
        }

        return ValueTask.FromResult<IEnumerable<Variant>>(defaultVariants);
    }
}
