// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Providers;
using Microsoft.Extensions.Configuration;
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
    /// Configures the integration between Excos.Options.Contextual and GrowthBook.
    /// </summary>
    public static IServiceCollection ConfigureExcosWithGrowthBook(this IServiceCollection services)
    {
        services.AddOptions<GrowthBookOptions>()
            .Validate(options => !string.IsNullOrEmpty(options.ClientKey));

        // Filter out the HttpClient logs for this library
        services.AddLogging(builder => builder.AddFilter((_, category, _) => category?.StartsWith($"System.Net.Http.HttpClient.{nameof(GrowthBook)}") != true));

        services.AddHttpClient(nameof(GrowthBook));
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
}
