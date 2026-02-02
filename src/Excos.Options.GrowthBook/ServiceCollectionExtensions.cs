// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Extension methods for configuring Excos.Options.Contextual with GrowthBook.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the integration between Excos.Options.Contextual and GrowthBook.
    /// </summary>
    public static IServiceCollection ConfigureExcosWithGrowthBook(this IServiceCollection services)
    {
        services.AddOptions<GrowthBookOptions>()
            .Validate(options => !string.IsNullOrEmpty(options.ClientKey));

        // Filter out the HttpClient logs for this library
        services.AddLogging(builder => builder.AddFilter((_, category, _) => category?.StartsWith($"System.Net.Http.HttpClient.{nameof(GrowthBook)}") != true));

        services.AddHttpClient(nameof(GrowthBook));
        services.AddSingleton<IGrowthBookHttpClientProvider, GrowthBookHttpClientFactory>();
        services.AddSingleton<GrowthBookApiCaller>();
        services.AddSingleton<GrowthBookFeatureCache>();
        services.AddHostedService(services => services.GetRequiredService<GrowthBookFeatureCache>()); // register cache as a service to get initialized on startup
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(GrowthBookFeatureProvider), ServiceLifetime.Singleton));

        return services;
    }

    /// <summary>
    /// Configures the integration between Excos.Options.Contextual and GrowthBook.
    /// </summary>
    public static IHostBuilder ConfigureExcosWithGrowthBook(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((_, services) =>
        {
            services.ConfigureExcosWithGrowthBook();
        });
        
        return hostBuilder;
    }

    /// <summary>
    /// Adds GrowthBook as a configuration source with the specified options.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">Action to configure GrowthBook options.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddExcosGrowthBookConfiguration(
        this IConfigurationBuilder builder,
        Action<GrowthBookConfigurationOptions> configure)
    {
        var options = new GrowthBookConfigurationOptions();
        configure(options);

        if (string.IsNullOrEmpty(options.ClientKey))
            throw new ArgumentException("ClientKey is required", nameof(configure));

        // Create standalone HTTP client provider
        var httpClientProvider = new GrowthBookHttpClientProvider(options.HttpMessageHandler);

        // Create null logger (no logging in standalone mode)
        var nullLoggerFactory = new NullLoggerFactory();
        var apiCallerLogger = nullLoggerFactory.CreateLogger<GrowthBookApiCaller>();
        var cacheLogger = nullLoggerFactory.CreateLogger<GrowthBookFeatureCache>();

        // Create options monitor
        var growthBookOptions = new GrowthBookOptions
        {
            ClientKey = options.ClientKey,
            RequestFeaturesOnInitialization = true,
            CacheDuration = TimeSpan.MaxValue // We don't need cache refresh - ExcosConfigurationProvider handles it
        };
        
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            // ApiKey is used as the API host in GrowthBook (non-standard naming)
            growthBookOptions.ApiHost = new Uri(options.ApiKey);
        }
        
        var optionsMonitor = new OptionsMonitor<GrowthBookOptions>(growthBookOptions);

        // Create API caller and cache
        var apiCaller = new GrowthBookApiCaller(httpClientProvider, apiCallerLogger, optionsMonitor);
        var cache = new GrowthBookFeatureCache(optionsMonitor, apiCaller, cacheLogger);

        // Trigger ExecuteAsync manually to load features
        var cts = new CancellationTokenSource();
        var executeTask = ((IHostedService)cache).StartAsync(cts.Token);
        executeTask.Wait(); // Wait for initial load

        // Create feature provider
        var featureProvider = new GrowthBookFeatureProvider(cache);

        // Add to configuration
        builder.AddExcosConfiguration(
            context: options.Context,
            featureProvider: featureProvider,
            refreshPeriod: options.RefreshPeriod);

        return builder;
    }
}

/// <summary>
/// Simple implementation of IOptionsMonitor for standalone scenarios.
/// </summary>
internal class OptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _value;

    public OptionsMonitor(T value)
    {
        _value = value;
    }

    public T CurrentValue => _value;
    public T Get(string? name) => _value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>
/// Null logger factory for standalone scenarios.
/// </summary>
internal class NullLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider) { }
    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
    public void Dispose() { }
}

/// <summary>
/// Null logger implementation.
/// </summary>
internal class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
