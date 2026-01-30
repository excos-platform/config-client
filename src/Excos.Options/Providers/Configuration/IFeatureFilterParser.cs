// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;
using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Providers.Configuration;

/// <summary>
/// A parser which can read a feature filter from a configuration source.
/// </summary>
public interface IFeatureFilterParser
{
    /// <summary>
    /// Tries to process the <paramref name="configuration"/> section and create a filtering condition.
    /// The provided section either has a string value or represents a nested object.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    /// <param name="configuration">Configuration section.</param>
    /// <param name="filteringCondition">A parsed filtering condition.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    bool TryParseFilter(string propertyName, IConfiguration configuration, [NotNullWhen(true)] out IFilteringCondition? filteringCondition);
}
