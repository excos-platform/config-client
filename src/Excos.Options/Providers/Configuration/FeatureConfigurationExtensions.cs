// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Excos.Options.Providers.Configuration.FilterParsers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excos.Options.Providers.Configuration;

public static class FeatureConfigurationExtensions
{
    public static void ConfigureExcosFeatures(this IServiceCollection services, string sectionName)
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureFilterParser), typeof(StringFilterParser), ServiceLifetime.Singleton));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureFilterParser), typeof(RangeFilterParser), ServiceLifetime.Singleton));

        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(OptionsFeatureProvider), ServiceLifetime.Singleton));
        services.AddOptions<FeatureCollection>()
            .Configure<IEnumerable<IFeatureFilterParser>, IConfiguration>((features, filterParsers, configuration) =>
            {
                filterParsers = filterParsers.Reverse().ToList(); // filters added last should be tried first
                configuration = configuration.GetSection(sectionName);
                foreach (var section in configuration.GetChildren())
                {
                    const string providerName = "Configuration";
                    var featureName = section.Key;

                    if (features.Contains(featureName))
                    {
                        // skip adding the same feature more than once in case someone calls this method more than once
                        continue;
                    }

                    var enabled = section.GetValue<bool?>("Enabled") ?? true;
                    var salt = section.GetValue<string?>("Salt");
                    var filters = LoadFilters(filterParsers, section.GetSection("Filters"));
                    var variants = LoadVariants(filterParsers, section.GetSection("Variants"));

                    var feature = new Feature
                    {
                        Name = featureName,
                        ProviderName = providerName,
                        Enabled = enabled,
                        Salt = salt!, // internally salt can be null
                    };
                    feature.Filters.AddRange(filters);
                    feature.Variants.AddRange(variants);

                    features.Add(feature);
                }
            });
    }

    private static IEnumerable<Variant> LoadVariants(IEnumerable<IFeatureFilterParser> filterParsers, IConfiguration variantsConfiguration)
    {
        foreach (var section in variantsConfiguration.GetChildren())
        {
            var variantId = section.Key;
            var allocationString = section.GetValue<string?>("Allocation")?.Trim();
            if (!Range<double>.TryParse(allocationString, null, out var range))
            {
                // alternative %
                if (allocationString != null && allocationString[^1] == '%' &&
                    double.TryParse(allocationString[..^1], out var percentage) && percentage > 0)
                {
                    range = new Range<double>(0, percentage / 100, RangeType.IncludeBoth);
                }
                else
                {
                    continue; // could not parse allocation
                }
            }

            if (range.Start < 0 || range.Start > 1 || range.End < 0 || range.End > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(range), $"(Variant: {variantId}) Allocation must be a range between 0 and 1.");
            }

            var allocation = new Allocation(range);
            var priority = section.GetValue<int?>("Priority");
            var filters = LoadFilters(filterParsers, section.GetSection("Filters"));
            var configuration = new ConfigurationBasedConfigureOptions(section.GetSection("Settings"));

            var variant = new Variant
            {
                Id = variantId,
                Allocation = allocation,
                Configuration = configuration,
                Priority = priority,
            };
            variant.Filters.AddRange(filters);

            yield return variant;
        }
    }
    private static IEnumerable<Filter> LoadFilters(IEnumerable<IFeatureFilterParser> filterParsers, IConfiguration filtersConfiguration)
    {
        foreach (var section in filtersConfiguration.GetChildren())
        {
            var propertyName = section.Key;
            
            IEnumerable<IFilteringCondition>? filteringConditions = null;
            
            var children = section.GetChildren();
            if (children.FirstOrDefault() is IConfigurationSection child && child.Key == "0")
            {
                // this is an array, thus we must parse values within to get OR treatment
                filteringConditions = children.Select(c => ParseFilter(filterParsers, c))
                    .Where(f => f != null).Cast<IFilteringCondition>();
            }
            
            if (filteringConditions == null)
            {
                // this is a single value, let's try to parse it
                var filteringCondition = ParseFilter(filterParsers, section);
                if (filteringCondition != null)
                {
                    filteringConditions = new[] { filteringCondition };
                }
            }

            // if no condition was parsed we will prevent running this filter
            if (filteringConditions != null)
            {
                var filter = new Filter
                {
                    PropertyName = propertyName,
                };
                filter.Conditions.AddRange(filteringConditions);
                yield return filter;
            }
            else
            {
                yield return new Filter
                {
                    PropertyName = propertyName,
                    Conditions = { NeverFilteringCondition.Instance }
                };
            }
        }
    }

    private static IFilteringCondition? ParseFilter(IEnumerable<IFeatureFilterParser> filterParsers, IConfiguration configuration)
    {
        foreach (var parser in filterParsers)
        {
            if (parser.TryParseFilter(configuration, out var filteringCondition))
            {
                return filteringCondition;
            }
        }

        return null;
    }
}
