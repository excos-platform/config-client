// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Excos.Options.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excos.Options.Providers;

/// <summary>
/// Builder for creating feature configurations using the Options framework.
/// </summary>
public sealed class OptionsFeatureBuilder
{
    private readonly OptionsBuilder<List<Feature>> _optionsBuilder;

    internal Feature Feature { get; }
    internal string ProviderName { get; }
    internal List<IFilteringCondition> Filters { get; } = [];

    internal OptionsFeatureBuilder(OptionsBuilder<List<Feature>> optionsBuilder, string featureName, string providerName)
    {
        _optionsBuilder = optionsBuilder;
        ProviderName = providerName;
        Feature = new Feature
        {
            Name = featureName,
        };
    }

    /// <summary>
    /// Saves the feature to the collection.
    /// </summary>
    /// <returns>Options builder for further method chaining.</returns>
    public OptionsBuilder<List<Feature>> Save()
    {
        foreach (var variant in Feature)
        {
            variant.Filters = variant.Filters.Concat(Filters).ToList();
        }
        return _optionsBuilder.Configure(features => features.Add(Feature));
    }
}

/// <summary>
/// Builder for creating feature filters using the Options framework.
/// </summary>
public sealed class OptionsFeatureFilterBuilder
{
    internal string PropertyName { get; }
    internal OptionsFeatureBuilder FeatureBuilder { get; }
    internal List<IFilteringCondition> Filter { get; } = [];

    internal OptionsFeatureFilterBuilder(OptionsFeatureBuilder featureBuilder, string propertyName)
    {
        FeatureBuilder = featureBuilder;
        PropertyName = propertyName;
    }

    /// <summary>
    /// Saves the filter to the feature.
    /// </summary>
    /// <returns>Feature builder for further method chaining.</returns>
    public OptionsFeatureBuilder SaveFilter()
    {
        FeatureBuilder.Filters.Add(Filter.Count == 0
            ? NeverFilteringCondition.Instance
            : Filter.Count == 1
                ? Filter[0]
                : new OrFilteringCondition(Filter.ToArray()));

        return FeatureBuilder;
    }
}

/// <summary>
/// Extension methods for configuring Excos features using the Options framework.
/// </summary>
public static class OptionsFeatureProviderBuilderExtensions
{
    /// <summary>
    /// Adds the Excos Options framework based feature provider to the services collection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddExcosOptionsFeatureProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(OptionsFeatureProvider), ServiceLifetime.Singleton));
        return services;
    }

    /// <summary>
    /// Creates a feature builder using the specified feature name.
    /// </summary>
    /// <param name="services">Services collection.</param>
    /// <param name="featureName">Feature name.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureBuilder BuildFeature(this IServiceCollection services, string featureName)
        => services.BuildFeature(featureName, Assembly.GetCallingAssembly().GetName()?.Name ?? nameof(OptionsFeatureBuilder));

    /// <summary>
    /// Creates a feature builder using the specified feature name and a custom provider name.
    /// </summary>
    /// <param name="services">Services collection.</param>
    /// <param name="featureName">Feature name.</param>
    /// <param name="providerName">Provider name.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureBuilder BuildFeature(this IServiceCollection services, string featureName, string providerName)
    {
        services.AddExcosOptionsFeatureProvider();
        return new OptionsFeatureBuilder(
            services.AddOptions<List<Feature>>(),
            featureName,
            providerName);
    }

    /// <summary>
    /// Creates a feature builder using the specified feature name.
    /// </summary>
    /// <param name="optionsBuilder">Options builder.</param>
    /// <param name="featureName">Feature name.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureBuilder BuildFeature(this OptionsBuilder<List<Feature>> optionsBuilder, string featureName) =>
        optionsBuilder.BuildFeature(featureName, Assembly.GetCallingAssembly().GetName()?.Name ?? nameof(OptionsFeatureBuilder));

    /// <summary>
    /// Creates a feature builder using the specified feature name and a custom provider name.
    /// </summary>
    /// <param name="optionsBuilder">Options builder.</param>
    /// <param name="featureName">Feature name.</param>
    /// <param name="providerName">Provider name.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureBuilder BuildFeature(this OptionsBuilder<List<Feature>> optionsBuilder, string featureName, string providerName)
    {
        optionsBuilder.Services.AddExcosOptionsFeatureProvider();
        return new OptionsFeatureBuilder(optionsBuilder, featureName, providerName);
    }

    /// <summary>
    /// Configures the feature properties.
    /// </summary>
    /// <param name="optionsFeatureBuilder">Builder.</param>
    /// <param name="action">Configuration callback.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureBuilder Configure(this OptionsFeatureBuilder optionsFeatureBuilder, Action<Feature> action)
    {
        action(optionsFeatureBuilder.Feature);
        return optionsFeatureBuilder;
    }

    /// <summary>
    /// Starts building a filter for the feature for a specific property.
    /// </summary>
    /// <param name="optionsFeatureBuilder">Builder.</param>
    /// <param name="propertyName">Property being filtered.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureFilterBuilder WithFilter(this OptionsFeatureBuilder optionsFeatureBuilder, string propertyName) =>
        new OptionsFeatureFilterBuilder(optionsFeatureBuilder, propertyName);

    /// <summary>
    /// No-op method for chaining of filter conditions.
    /// </summary>
    /// <param name="builder">Builder.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureFilterBuilder Or(this OptionsFeatureFilterBuilder builder) => builder; // no-op

    /// <summary>
    /// Adds a condition to the filter that checks if the property matches the specified value.
    /// </summary>
    /// <param name="builder">Builder.</param>
    /// <param name="value">String to match (case-insensitive, culture-invariant).</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureFilterBuilder Matches(this OptionsFeatureFilterBuilder builder, string value)
    {
        builder.Filter.Add(new StringFilteringCondition(builder.PropertyName, value));
        return builder;
    }

    /// <summary>
    /// Adds a condition to the filter that checks if the property matches the specified regular expresion.
    /// </summary>
    /// <param name="builder">Builder.</param>
    /// <param name="pattern">Regex to match (case-insensitive, culture-invariant).</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureFilterBuilder RegexMatches(this OptionsFeatureFilterBuilder builder, string pattern)
    {
        builder.Filter.Add(new RegexFilteringCondition(builder.PropertyName, pattern));
        return builder;
    }

    /// <summary>
    /// Adds a condition to the filter that checks if the property fits into the specified range.
    /// </summary>
    /// <param name="builder">Builder.</param>
    /// <param name="range">Range to check the value against.</param>
    /// <returns>Builder.</returns>
    public static OptionsFeatureFilterBuilder InRange<T>(this OptionsFeatureFilterBuilder builder, Range<T> range)
        where T : IComparable<T>, ISpanParsable<T>
    {
        builder.Filter.Add(new RangeFilteringCondition<T>(builder.PropertyName, range));
        return builder;
    }

    /// <summary>
    /// Sets up an A/B experiment for the feature with JSON configuration.
    /// </summary>
    /// <param name="optionsFeatureBuilder">Builder.</param>
    /// <param name="configurationJsonA">JSON configuration string for variant A.</param>
    /// <param name="configurationJsonB">JSON configuration string for variant B.</param>
    /// <param name="allocationUnit">Property of the context used for allocation.</param>
    /// <returns>Builder.</returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON configuration is invalid.</exception>
    public static OptionsFeatureBuilder ABExperiment(
        this OptionsFeatureBuilder optionsFeatureBuilder, 
        string configurationJsonA, 
        string configurationJsonB, 
        string allocationUnit = "UserId")
    {
        var configA = configurationJsonA.ParseAsJsonElement();
        var configB = configurationJsonB.ParseAsJsonElement();
        
        optionsFeatureBuilder.Feature.Add(new Variant
        {
            Id = $"{optionsFeatureBuilder.Feature.Name}:A_{optionsFeatureBuilder.Feature.Count}",
            Filters = [
                new AllocationFilteringCondition(
                    allocationUnit,
                    $"{optionsFeatureBuilder.ProviderName}_{optionsFeatureBuilder.Feature.Name}",
                    XxHashAllocation.Instance,
                    new Allocation(new Range<double>(0, 0.5, RangeType.IncludeStart)))
                ],
            Configuration = configA,
        });
        optionsFeatureBuilder.Feature.Add(new Variant
        {
            Id = $"{optionsFeatureBuilder.Feature.Name}:B_{optionsFeatureBuilder.Feature.Count}",
            Filters = [
                new AllocationFilteringCondition(
                    allocationUnit,
                    $"{optionsFeatureBuilder.ProviderName}_{optionsFeatureBuilder.Feature.Name}",
                    XxHashAllocation.Instance,
                    new Allocation(new Range<double>(0.5, 1, RangeType.IncludeBoth)))
                ],
            Configuration = configB,
        });

        return optionsFeatureBuilder;
    }

    /// <summary>
    /// Sets up a feature rollout for a specific percentage of the population with JSON configuration.
    /// </summary>
    /// <param name="optionsFeatureBuilder">Builder.</param>
    /// <param name="percentage">Rollout percentage (0-100%)</param>
    /// <param name="configurationJson">JSON configuration string.</param>
    /// <param name="allocationUnit">Property of the context used for allocation.</param>
    /// <returns>Builder.</returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON configuration is invalid.</exception>
    public static OptionsFeatureBuilder Rollout(
        this OptionsFeatureBuilder optionsFeatureBuilder, 
        double percentage, 
        string configurationJson, 
        string allocationUnit = "UserId")
    {
        var config = configurationJson.ParseAsJsonElement();
        
        optionsFeatureBuilder.Feature.Add(new Variant
        {
            Id = $"{optionsFeatureBuilder.Feature.Name}:Rollout_{optionsFeatureBuilder.Feature.Count}",
            Filters = [
                new AllocationFilteringCondition(
                    allocationUnit,
                    $"{optionsFeatureBuilder.ProviderName}_{optionsFeatureBuilder.Feature.Name}",
                    XxHashAllocation.Instance, 
                    Allocation.Percentage(percentage))
                ],
            Configuration = config,
        });

        return optionsFeatureBuilder;
    }
}
