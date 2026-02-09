// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Contextual;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Configuration;

/// <summary>
/// Extension methods for adding Excos configuration to <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class ExcosConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds Excos feature configuration using the specified feature providers and context.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="featureProviders">Feature providers to load variants from.</param>
    /// <param name="context">Context used for variant filtering.</param>
    /// <param name="refreshPeriod">Optional period for automatic refresh.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddExcosConfiguration(
        this IConfigurationBuilder builder,
        IEnumerable<IFeatureProvider> featureProviders,
        IOptionsContext context,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(featureProviders);
        ArgumentNullException.ThrowIfNull(context);

        builder.Add(new ExcosConfigurationSource
        {
            FeatureProviders = featureProviders,
            Context = context,
            RefreshPeriod = refreshPeriod
        });

        return builder;
    }

    /// <summary>
    /// Adds Excos feature configuration using the specified feature providers and a dictionary context.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="featureProviders">Feature providers to load variants from.</param>
    /// <param name="context">Dictionary of context values used for variant filtering.</param>
    /// <param name="refreshPeriod">Optional period for automatic refresh.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddExcosConfiguration(
        this IConfigurationBuilder builder,
        IEnumerable<IFeatureProvider> featureProviders,
        IDictionary<string, string> context,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return builder.AddExcosConfiguration(
            featureProviders,
            new DictionaryOptionsContext(context),
            refreshPeriod);
    }

    /// <summary>
    /// Adds Excos feature configuration using a single feature provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="featureProvider">Feature provider to load variants from.</param>
    /// <param name="context">Context used for variant filtering.</param>
    /// <param name="refreshPeriod">Optional period for automatic refresh.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddExcosConfiguration(
        this IConfigurationBuilder builder,
        IFeatureProvider featureProvider,
        IOptionsContext context,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(featureProvider);

        return builder.AddExcosConfiguration(
            new[] { featureProvider },
            context,
            refreshPeriod);
    }

    /// <summary>
    /// Adds Excos feature configuration using a single feature provider and dictionary context.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="featureProvider">Feature provider to load variants from.</param>
    /// <param name="context">Dictionary of context values used for variant filtering.</param>
    /// <param name="refreshPeriod">Optional period for automatic refresh.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddExcosConfiguration(
        this IConfigurationBuilder builder,
        IFeatureProvider featureProvider,
        IDictionary<string, string> context,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(featureProvider);
        ArgumentNullException.ThrowIfNull(context);

        return builder.AddExcosConfiguration(
            new[] { featureProvider },
            new DictionaryOptionsContext(context),
            refreshPeriod);
    }
}
