# Decision 004: HTTP Client Abstraction for GrowthBook

**Date**: 2026-02-02  
**Status**: Implemented  
**Context**: Refactoring for extensibility and clean architecture

## Problem Statement

The current `GrowthBookApiCaller` depends on `IHttpClientFactory` obtained through DI:

```csharp
internal class GrowthBookApiCaller
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public GrowthBookApiCaller(IHttpClientFactory httpClientFactory, ...)
    {
        _httpClientFactory = httpClientFactory;
    }
}
```

This creates issues for standalone usage:
- Cannot use GrowthBook configuration provider without full DI setup
- `services.AddHttpClient()` is required before GrowthBook can work

## Decision

### Use `IHttpClientFactory` as the abstraction

Keep using the standard `IHttpClientFactory` interface for compatibility with external systems that may provide their own implementation.

### Create a simple standalone implementation

```csharp
internal class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler? _handler;
    
    public SimpleHttpClientFactory(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }
    
    public HttpClient CreateClient(string name)
    {
        // New HttpClient per request
        // Handler pools connections internally (SocketsHttpHandler default behavior)
        return _handler != null 
            ? new HttpClient(_handler, disposeHandler: false) 
            : new HttpClient();
    }
}
```

### Configuration options integration

```csharp
public class GrowthBookOptions
{
    public Uri ApiHost { get; set; } = new Uri("https://cdn.growthbook.io");
    public string ClientKey { get; set; } = string.Empty;
    public IDictionary<string, string>? Context { get; set; }
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
    public bool RequestFeaturesOnInitialization { get; set; } = true;
    
    /// <summary>
    /// Optional HTTP message handler for customizing HTTP behavior.
    /// Used when HttpClientFactory is not provided.
    /// The caller owns the handler lifecycle.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }
    
    /// <summary>
    /// Optional HTTP client factory. Takes precedence over HttpMessageHandler.
    /// Use this for advanced scenarios or DI integration.
    /// </summary>
    public IHttpClientFactory? HttpClientFactory { get; set; }
}
```

### Factory resolution logic

```csharp
// In AddExcosGrowthBookConfiguration extension method:
IHttpClientFactory factory = options.HttpClientFactory 
    ?? new SimpleHttpClientFactory(options.HttpMessageHandler);
```

### HTTP client lifecycle

1. **New `HttpClient` per request** - Matches `IHttpClientFactory` pattern
2. **Do not dispose `HttpClient`** - Let GC handle it; `SocketsHttpHandler` manages connection pooling
3. **Do not dispose `HttpMessageHandler`** - Caller owns it (passed with `disposeHandler: false`)
4. **Do not dispose `IHttpClientFactory`** - Caller owns it

### Rationale for per-request HttpClient

- Modern .NET (`SocketsHttpHandler`) handles connection pooling at the handler level
- Creating new `HttpClient` per request avoids DNS caching issues
- Matches the pattern established by `IHttpClientFactory`
- No socket exhaustion concerns (handler pools connections)

## Consequences

1. **Standalone usage works** - Can use GrowthBook without DI setup
2. **Backward compatible** - Existing DI-based code continues to work
3. **Flexible customization** - Provide handler for proxies, custom certificates, etc.
4. **Standard interface** - External systems can provide `IHttpClientFactory`

## Files Affected

### New Files
- `Excos.Options.GrowthBook/SimpleHttpClientFactory.cs` - Standalone `IHttpClientFactory` implementation
- `Excos.Options.GrowthBook/StaticOptionsMonitor.cs` - Simple `IOptionsMonitor<T>` for standalone scenarios

### Modified Files
- `Excos.Options.GrowthBook/GrowthBookOptions.cs` - Unified options with HTTP customization properties
- `Excos.Options.GrowthBook/GrowthBookApiCaller.cs` - Uses `IHttpClientFactory` (kept for separation of concerns)
- `Excos.Options.GrowthBook/GrowthBookFeatureProvider.cs` - Uses `GrowthBookApiCaller` for HTTP operations
- `Excos.Options.GrowthBook/ServiceCollectionExtensions.cs` - Wire up factory appropriately

### Architecture Note
`GrowthBookApiCaller` is kept as a separate class to maintain separation of concerns:
- `GrowthBookFeatureProvider` - Caching, feature conversion, `IFeatureProvider` contract
- `GrowthBookApiCaller` - HTTP communication, response parsing, error handling

## Testing Strategy

1. **Standalone factory tests**:
   - Creates new HttpClient per request
   - Uses provided handler when specified
   - Uses default handler when none provided

2. **Integration with configuration callback**:
   - Custom HttpMessageHandler is used for requests
   - Custom IHttpClientFactory takes precedence
   - Default behavior works when neither specified

3. **Mock handler tests**:
   - Provider correctly calls API with mock handler
   - Response parsing works as expected
