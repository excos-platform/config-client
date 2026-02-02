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
    /// Configures the integration between Excos.Options.Contextual and GrowthBook as well as registering a Configuration provider for GrowthBook features' default values.
    /// </summary>
    public static IHostBuilder ConfigureExcosWithGrowthBook(this IHostBuilder hostBuilder)
    {
        // Create the shared feature evaluation for default values
        var gbEvaluation = new GrowthBookDefaultValuesFeatureEvaluation();
        
        hostBuilder.ConfigureAppConfiguration((_, builder) =>
        {
            // Add configuration provider for GrowthBook default values
            // Empty dictionary context: default values have no filter requirements (match all contexts)
            // Periodic refresh: picks up changes when GrowthBookFeatureCache updates features
            builder.AddExcosConfiguration(
                new Dictionary<string, string>(), // Empty context - default values match everything
                gbEvaluation,
                TimeSpan.FromSeconds(1));
        });
        
        hostBuilder.ConfigureServices((_, services) =>
        {
            services.ConfigureExcosWithGrowthBook();
            // Register the evaluation so GrowthBookFeatureCache can update it
            services.AddSingleton(gbEvaluation);
        });
        
        return hostBuilder;
    }
}
