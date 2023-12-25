// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;
using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Providers.Configuration;

public interface IFeatureFilterParser
{
    bool TryParseFilter(IConfiguration configuration, [NotNullWhen(true)] out IFilteringCondition? filteringCondition);
}
