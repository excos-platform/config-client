// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;

namespace Excos.Options.GrowthBook;

internal class GrowthBookFeatureProvider : IFeatureProvider
{
    private readonly GrowthBookFeatureCache _featureCache;

    public GrowthBookFeatureProvider(GrowthBookFeatureCache featureCache)
    {
        _featureCache = featureCache;
    }

    public ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        return _featureCache.GetFeaturesAsync();
    }
}
