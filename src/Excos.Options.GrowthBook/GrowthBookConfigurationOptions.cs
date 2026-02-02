// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook;

/// <summary>
/// Configuration options for adding GrowthBook as a configuration source.
/// </summary>
public class GrowthBookConfigurationOptions
{
    /// <summary>
    /// The GrowthBook API host (optional, defaults to https://cdn.growthbook.io).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The GrowthBook client key (required).
    /// </summary>
    public string ClientKey { get; set; } = string.Empty;

    /// <summary>
    /// The context dictionary for filtering variants.
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// Optional refresh period for reloading features. If null, features are loaded once.
    /// </summary>
    public TimeSpan? RefreshPeriod { get; set; }

    /// <summary>
    /// Optional custom HTTP message handler for the HTTP client.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }
}
