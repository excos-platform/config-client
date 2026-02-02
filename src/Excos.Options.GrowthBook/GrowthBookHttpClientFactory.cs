// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook;

/// <summary>
/// HTTP client provider that uses IHttpClientFactory.
/// This is used when GrowthBook is configured through DI services.
/// </summary>
internal class GrowthBookHttpClientFactory : IGrowthBookHttpClientProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GrowthBookHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient GetHttpClient()
    {
        return _httpClientFactory.CreateClient(nameof(GrowthBook));
    }
}
