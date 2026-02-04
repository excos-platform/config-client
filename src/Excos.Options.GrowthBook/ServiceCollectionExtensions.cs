// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Extension methods for configuring Excos.Options.Contextual with GrowthBook.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the integration between Excos and GrowthBook for both configuration and contextual options.
    /// Creates a shared feature provider instance used by both systems.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="configure">Callback to configure GrowthBook options.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder ConfigureExcosWithGrowthBook(this IHostBuilder hostBuilder, Action<GrowthBookOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GrowthBookOptions();
        configure(options);

        // Create the shared feature provider
        var featureProvider = GrowthBookConfigurationBuilderExtensions.CreateFeatureProvider(options);

        // Add GrowthBook as a configuration source
        hostBuilder.ConfigureAppConfiguration((_, builder) =>
        {
            builder.AddExcosGrowthBookConfiguration(featureProvider, options);
        });

        // Register the shared provider for contextual options
        hostBuilder.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IFeatureProvider>(featureProvider);
        });

        return hostBuilder;
    }

    /// <summary>
    /// Configures the integration between Excos.Options.Contextual and GrowthBook using dependency injection.
    /// Use this when you need DI-managed HTTP clients and logging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback to configure GrowthBook options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureExcosWithGrowthBook(this IServiceCollection services, Action<GrowthBookOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GrowthBookOptions();
        configure(options);

        services.AddOptions<GrowthBookOptions>()
            .Configure(configure)
            .Validate(opts => !string.IsNullOrEmpty(opts.ClientKey));

        // Filter out the HttpClient logs for this library
        services.AddLogging(builder => builder.AddFilter((_, category, _) => category?.StartsWith($"System.Net.Http.HttpClient.{nameof(GrowthBook)}") != true));

        // Use custom HTTP client factory if provided, otherwise register standard one
        if (options.HttpClientFactory != null)
        {
            services.AddSingleton<IHttpClientFactory>(options.HttpClientFactory);
        }
        else if (options.HttpMessageHandler != null)
        {
            services.AddSingleton<IHttpClientFactory>(new SimpleHttpClientFactory(options.HttpMessageHandler));
        }
        else
        {
            services.AddHttpClient(nameof(GrowthBook));
        }

        services.AddSingleton<GrowthBookApiCaller>();
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(GrowthBookFeatureProvider), ServiceLifetime.Singleton));

        return services;
    }
}
