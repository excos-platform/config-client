// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Contextual;
using Excos.Options.Providers.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excos.Options.GrowthBook;

/// <summary>
/// Extension methods for adding GrowthBook configuration to <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class GrowthBookConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds GrowthBook feature configuration as a configuration source.
    /// This is a standalone configuration method that doesn't require dependency injection.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">Callback to configure GrowthBook options.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddExcosGrowthBookConfiguration(
        this IConfigurationBuilder builder,
        Action<GrowthBookOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GrowthBookOptions();
        configure(options);

        var featureProvider = CreateFeatureProvider(options);

        return AddExcosGrowthBookConfiguration(builder, featureProvider, options);
    }

    /// <summary>
    /// Adds GrowthBook feature configuration using an existing feature provider.
    /// Used internally for sharing a provider between configuration and contextual options.
    /// </summary>
    internal static IConfigurationBuilder AddExcosGrowthBookConfiguration(
        this IConfigurationBuilder builder,
        IFeatureProvider featureProvider,
        GrowthBookOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(featureProvider);
        ArgumentNullException.ThrowIfNull(options);

        // Create the context from the options
        var context = options.Context != null
            ? new DictionaryOptionsContext(options.Context)
            : new DictionaryOptionsContext();

        // Use the generic Excos configuration extension
        return builder.AddExcosConfiguration(
            new[] { featureProvider },
            context,
            options.CacheDuration);
    }

    /// <summary>
    /// Creates a standalone GrowthBook feature provider.
    /// </summary>
    internal static GrowthBookFeatureProvider CreateFeatureProvider(GrowthBookOptions options)
    {
        if (string.IsNullOrEmpty(options.ClientKey))
        {
            throw new ArgumentException("ClientKey must be specified.", nameof(options));
        }

        // Create HTTP client factory - use provided one or create standalone
        var httpClientFactory = options.HttpClientFactory
            ?? new SimpleHttpClientFactory(options.HttpMessageHandler);

        // Create dependencies
        var optionsMonitor = new StaticOptionsMonitor<GrowthBookOptions>(options);
        var apiCallerLogger = NullLogger<GrowthBookApiCaller>.Instance;
        var providerLogger = NullLogger<GrowthBookFeatureProvider>.Instance;

        var apiCaller = new GrowthBookApiCaller(httpClientFactory, apiCallerLogger, optionsMonitor);

        return new GrowthBookFeatureProvider(optionsMonitor, apiCaller, providerLogger);
    }
}
