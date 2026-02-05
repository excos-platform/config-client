// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Providers.Configuration;

/// <summary>
/// Configuration source for building an <see cref="ExcosConfigurationProvider"/>.
/// </summary>
internal class ExcosConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Feature providers to load variants from.
    /// </summary>
    public IEnumerable<IFeatureProvider> FeatureProviders { get; set; } = Array.Empty<IFeatureProvider>();

    /// <summary>
    /// Context used for variant filtering.
    /// </summary>
    public IOptionsContext Context { get; set; } = null!;

    /// <summary>
    /// Optional period for automatic refresh. If null, configuration loads once.
    /// </summary>
    public TimeSpan? RefreshPeriod { get; set; }

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        // Create a FeatureEvaluation instance from the feature providers
        var featureEvaluation = new FeatureEvaluation(FeatureProviders);
        return new ExcosConfigurationProvider(featureEvaluation, Context, RefreshPeriod);
    }
}
