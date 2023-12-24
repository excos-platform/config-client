// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Abstractions;

public interface IExperimentProvider
{
    Task<IEnumerable<Experiment>> GetExperimentsAsync(CancellationToken cancellationToken);
}
