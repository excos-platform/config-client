// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.ComponentModel.DataAnnotations;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Options for configuring GrowthBook integration with Excos.
/// </summary>
public class GrowthBookOptions
{
    /// <summary>
    /// Host of the GrowthBook API (the one from where we read features).
    /// </summary>
    public Uri ApiHost { get; set; } = new Uri("https://cdn.growthbook.io");

    /// <summary>
    /// Client key for the GrowthBook API.
    /// </summary>
    [Required]
    public string ClientKey { get; set; } = string.Empty;

    //public string? DecryptionKey { get; set; } // Only available in paid version

    /// <summary>
    /// How often we will refresh the features from the GrowthBook API.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether we should request features from the GrowthBook API on initialization or lazily upon first request of configuration.
    /// </summary>
    public bool RequestFeaturesOnInitialization { get; set; } = true;
}
