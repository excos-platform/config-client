// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Excos.Options.Providers.Configuration.FilterParsers;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excos.Options.Providers.Configuration;

/// <summary>
/// Extension methods for configuring Excos features using <see cref="IServiceCollection"/>.
/// </summary>
public static class FeatureConfigurationExtensions
{
    const string ProviderName = "Configuration";

    /// <summary>
    /// Configures Excos features from <see cref="Microsoft.Extensions.Configuration"/> using <paramref name="sectionName"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="sectionName">Section name to use to look up the configuration.</param>
    public static void ConfigureExcosFeatures(this IServiceCollection services, string sectionName)
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureFilterParser), typeof(StringFilterParser), ServiceLifetime.Singleton));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureFilterParser), typeof(RangeFilterParser), ServiceLifetime.Singleton));

        services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(OptionsFeatureProvider), ServiceLifetime.Singleton));
        services.AddOptions<List<Feature>>()
            .Configure<IEnumerable<IFeatureFilterParser>, IConfiguration>((features, filterParsers, configuration) =>
            {
                filterParsers = filterParsers.Reverse().ToList(); // filters added last should be tried first
                configuration = configuration.GetSection(sectionName);
                foreach (var section in configuration.GetChildren())
                {
                    var featureName = section.Key;

                    if (features.Any(f => f.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // skip adding the same feature more than once in case someone calls this method more than once
                        continue;
                    }

                    var enabled = section.GetValue<bool?>("Enabled") ?? true;
                    if (!enabled)
                    {
                        continue;
                    }

                    var salt = section.GetValue<string?>("Salt");
                    var filters = LoadFilters(filterParsers, section.GetSection("Filters")).ToList();
                    var variants = LoadVariants(filterParsers, featureName, salt ?? $"{ProviderName}_{featureName}", section.GetSection("Variants"));

                    var feature = new Feature
                    {
                        Name = featureName,
                    };

                    feature.AddRange(variants);

                    foreach (var variant in feature)
                    {
                        variant.Filters = variant.Filters.Concat(filters);
                    }


                    features.Add(feature);
                }
            });
    }

    private static IEnumerable<Variant> LoadVariants(IEnumerable<IFeatureFilterParser> filterParsers, string featureName, string salt, IConfiguration variantsConfiguration)
    {
        foreach (var section in variantsConfiguration.GetChildren())
        {
            var variantId = section.Key;
            var allocationUnit = section.GetValue<string?>("AllocationUnit")?.Trim() ?? "UserId";
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
            var filters = LoadFilters(filterParsers, section.GetSection("Filters")).ToList();
            var configuration = new ConfigurationBasedConfigureOptions(section.GetSection("Settings"));

            var variant = new Variant
            {
                Id = $"{featureName}:{variantId}",
                Configuration = configuration,
                // if priority is not specified, we give priority to variants with more filters
                Priority = priority ?? 1024 - filters.Count,
            };
            variant.Filters = [
                new AllocationFilteringCondition(allocationUnit, salt, XxHashAllocation.Instance, allocation),
                ..filters
            ];

            yield return variant;
        }
    }
    private static IEnumerable<IFilteringCondition> LoadFilters(IEnumerable<IFeatureFilterParser> filterParsers, IConfiguration filtersConfiguration)
    {
        foreach (var section in filtersConfiguration.GetChildren())
        {
            var propertyName = section.Key;

            IFilteringCondition? filteringCondition = null;

            var children = section.GetChildren();
            if (children.FirstOrDefault() is IConfigurationSection child && child.Key == "0")
            {
                // this is an array, thus we must parse values within to get OR treatment
                filteringCondition = new OrFilteringCondition(children.Select(c => ParseFilter(filterParsers, propertyName, c))
                    .Where(f => f != null).Cast<IFilteringCondition>().ToArray());
            }

            if (filteringCondition == null)
            {
                // this is a single value, let's try to parse it
                filteringCondition = ParseFilter(filterParsers, propertyName, section);
            }

            // if no condition was parsed we will prevent running this filter
            if (filteringCondition != null)
            {
                yield return filteringCondition;
            }
            else
            {
                yield return NeverFilteringCondition.Instance;
            }
        }
    }

    private static IFilteringCondition? ParseFilter(IEnumerable<IFeatureFilterParser> filterParsers, string propertyName, IConfiguration configuration)
    {
        foreach (var parser in filterParsers)
        {
            if (parser.TryParseFilter(propertyName, configuration, out var filteringCondition))
            {
                return filteringCondition;
            }
        }

        return null;
    }
}
