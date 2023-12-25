// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Abstractions;

public interface IFeatureProvider
{
    Task<IEnumerable<Feature>> GetExperimentsAsync(CancellationToken cancellationToken);
}
