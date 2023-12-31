// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excos.Options.Providers;

public sealed class OptionsFeatureBuilder
{
    private readonly OptionsBuilder<FeatureCollection> _optionsBuilder;

    internal Feature Feature { get; }

    internal OptionsFeatureBuilder(OptionsBuilder<FeatureCollection> optionsBuilder, string featureName, string providerName)
    {
        _optionsBuilder = optionsBuilder;
        Feature = new Feature
        {
            Name = featureName,
            ProviderName = providerName,
        };
    }

    public OptionsBuilder<FeatureCollection> Save() => 
        _optionsBuilder.Configure(features => features.Add(Feature));
}

public sealed class OptionsFeatureFilterBuilder
{
    internal OptionsFeatureBuilder FeatureBuilder { get; }
    
    internal Filter Filter { get; }
    
    internal OptionsFeatureFilterBuilder(OptionsFeatureBuilder featureBuilder, string propertyName)
    {
        FeatureBuilder = featureBuilder;
        Filter = new Filter
        {
            PropertyName = propertyName,
        };
    }

    public OptionsFeatureBuilder SaveFilter()
    {
        FeatureBuilder.Feature.Filters.Add(Filter);
        return FeatureBuilder;
    }
}

public static class OptionsFeatureProviderBuilderExtensions
{
    public static OptionsFeatureBuilder BuildFeature(this IServiceCollection services, string featureName)
        => services.BuildFeature(featureName, Assembly.GetCallingAssembly().GetName()?.Name ?? nameof(OptionsFeatureBuilder));

    public static OptionsFeatureBuilder BuildFeature(this IServiceCollection services, string featureName, string providerName)
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(OptionsFeatureProvider), ServiceLifetime.Singleton));
        return new OptionsFeatureBuilder(
            services.AddOptions<FeatureCollection>(),
            featureName,
            providerName);
    }

    public static OptionsFeatureBuilder BuildFeature(this OptionsBuilder<FeatureCollection> optionsBuilder, string featureName) =>
        optionsBuilder.BuildFeature(featureName, Assembly.GetCallingAssembly().GetName()?.Name ?? nameof(OptionsFeatureBuilder));

    public static OptionsFeatureBuilder BuildFeature(this OptionsBuilder<FeatureCollection> optionsBuilder, string featureName, string providerName)
    {
        optionsBuilder.Services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(OptionsFeatureProvider), ServiceLifetime.Singleton));
        return new OptionsFeatureBuilder(optionsBuilder, featureName, providerName);
    }

    public static OptionsFeatureBuilder Configure(this OptionsFeatureBuilder optionsFeatureBuilder, Action<Feature> action)
    {
        action(optionsFeatureBuilder.Feature);
        return optionsFeatureBuilder;
    }

    public static OptionsFeatureFilterBuilder WithFilter(this OptionsFeatureBuilder optionsFeatureBuilder, string propertyName) =>
        new OptionsFeatureFilterBuilder(optionsFeatureBuilder, propertyName);

    public static OptionsFeatureFilterBuilder Or(this OptionsFeatureFilterBuilder builder) => builder; // no-op

    public static OptionsFeatureFilterBuilder Matches(this OptionsFeatureFilterBuilder builder, string value)
    {
        builder.Filter.Conditions.Add(new StringFilteringCondition(value));
        return builder;
    }

    public static OptionsFeatureFilterBuilder RegexMatches(this OptionsFeatureFilterBuilder builder, string pattern)
    {
        builder.Filter.Conditions.Add(new RegexFilteringCondition(pattern));
        return builder;
    }

    public static OptionsFeatureFilterBuilder InRange<T>(this OptionsFeatureFilterBuilder builder, Range<T> range)
        where T : IComparable<T>, ISpanParsable<T>
    {
        builder.Filter.Conditions.Add(new RangeFilteringCondition<T>(range));
        return builder;
    }

    public static OptionsFeatureBuilder ABExperiment<TOptions>(this OptionsFeatureBuilder optionsFeatureBuilder, Action<TOptions, string> configureA, Action<TOptions, string> configureB)
    {
        optionsFeatureBuilder.Feature.Variants.Add(new Variant
        {
            Id = "A",
            Allocation = new Allocation(new Range<double>(0, 0.5, RangeType.IncludeStart)),
            Configuration = new CallbackConfigureOptions<TOptions>(configureA),
        });
        optionsFeatureBuilder.Feature.Variants.Add(new Variant
        {
            Id = "B",
            Allocation = new Allocation(new Range<double>(0.5, 1, RangeType.IncludeBoth)),
            Configuration = new CallbackConfigureOptions<TOptions>(configureB),
        });

        return optionsFeatureBuilder;
    }

    public static OptionsFeatureBuilder Rollout<TOptions>(this OptionsFeatureBuilder optionsFeatureBuilder, double percentage, Action<TOptions, string> configure)
    {
        optionsFeatureBuilder.Feature.Variants.Add(new Variant
        {
            Id = "Rollout",
            Allocation = Allocation.Percentage(percentage),
            Configuration = new CallbackConfigureOptions<TOptions>(configure),
        });

        return optionsFeatureBuilder;
    }
}

internal sealed class CallbackConfigureOptions<TDesignatedOptions> : IConfigureOptions
{
    private readonly Action<TDesignatedOptions, string> _configure;

    public CallbackConfigureOptions(Action<TDesignatedOptions, string> configure)
    {
        _configure = configure;
    }

    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        if (typeof(TOptions) == typeof(TDesignatedOptions))
        {
            _configure(Unsafe.As<TOptions, TDesignatedOptions>(ref input), section);
        }
    }
}
