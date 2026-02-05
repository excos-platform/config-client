// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook;

/// <summary>
/// A simple IHttpClientFactory implementation for standalone usage without DI.
/// </summary>
internal class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    /// Creates a new factory with an optional custom handler.
    /// </summary>
    /// <param name="handler">Optional HTTP message handler. The caller owns the handler lifecycle.</param>
    public SimpleHttpClientFactory(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    /// <inheritdoc/>
    public HttpClient CreateClient(string name)
    {
        // New HttpClient per request - matches IHttpClientFactory pattern
        // SocketsHttpHandler (default) manages connection pooling internally
        return _handler != null
            ? new HttpClient(_handler, disposeHandler: false)
            : new HttpClient();
    }
}
