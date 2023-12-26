// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Abstractions;

/// <summary>
/// A provider of feature configurations.
/// </summary>
public interface IFeatureProvider
{
    /// <summary>
    /// Gets a list of features from this provider.
    /// </summary>
    /// <remarks>
    /// It's on the provider to handle caching of the features if needed.
    /// </remarks> 
    Task<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken);
}
