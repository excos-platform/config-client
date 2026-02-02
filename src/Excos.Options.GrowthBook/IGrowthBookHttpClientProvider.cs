// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook;

/// <summary>
/// Provides HTTP clients for making requests to the GrowthBook API.
/// </summary>
internal interface IGrowthBookHttpClientProvider
{
    /// <summary>
    /// Gets an HTTP client for making requests to the GrowthBook API.
    /// The client should be disposed after use.
    /// </summary>
    /// <returns>An HTTP client instance.</returns>
    HttpClient GetHttpClient();
}
