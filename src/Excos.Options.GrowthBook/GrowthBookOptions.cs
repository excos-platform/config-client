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

    /// <summary>
    /// Context values for variant filtering (e.g., Market, Environment).
    /// Used when adding GrowthBook as a configuration source.
    /// </summary>
    public IDictionary<string, string>? Context { get; set; }

    /// <summary>
    /// Optional HTTP message handler for customizing HTTP behavior.
    /// Used when HttpClientFactory is not available (standalone scenarios).
    /// The caller owns the handler lifecycle.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>
    /// Optional HTTP client factory. Takes precedence over HttpMessageHandler.
    /// When null in standalone scenarios, a SimpleHttpClientFactory is created.
    /// </summary>
    public IHttpClientFactory? HttpClientFactory { get; set; }
}
