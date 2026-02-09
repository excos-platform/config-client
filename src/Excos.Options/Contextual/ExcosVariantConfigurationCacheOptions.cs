// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Contextual;

/// <summary>
/// Configuration options for the variant configuration cache.
/// </summary>
public class ExcosVariantConfigurationCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of cached configurations.
    /// Default is 16.
    /// </summary>
    public int MaxCacheSize { get; set; } = 16;
}
