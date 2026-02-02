// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook;

/// <summary>
/// HTTP client provider that creates HttpClient instances directly.
/// This is used for standalone configuration scenarios without DI.
/// Each call creates a new HttpClient instance that should be disposed after use.
/// </summary>
internal class GrowthBookHttpClientProvider : IGrowthBookHttpClientProvider
{
    private readonly HttpMessageHandler? _messageHandler;

    /// <summary>
    /// Creates a new instance of GrowthBookHttpClientProvider.
    /// </summary>
    /// <param name="messageHandler">Optional HTTP message handler. If not provided, a default handler is used.</param>
    public GrowthBookHttpClientProvider(HttpMessageHandler? messageHandler = null)
    {
        _messageHandler = messageHandler;
    }

    public HttpClient GetHttpClient()
    {
        if (_messageHandler != null)
        {
            return new HttpClient(_messageHandler, disposeHandler: false);
        }
        
        return new HttpClient();
    }
}
