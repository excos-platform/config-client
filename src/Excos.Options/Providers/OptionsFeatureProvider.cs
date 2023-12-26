// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options;

namespace Excos.Options.Providers;

public class OptionsFeatureProvider : IFeatureProvider
{
    private readonly IOptionsMonitor<FeatureCollection> _options;

    public OptionsFeatureProvider(IOptionsMonitor<FeatureCollection> options)
    {
        _options = options;
    }

    public Task<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<Feature>>(_options.CurrentValue);
    }
}
