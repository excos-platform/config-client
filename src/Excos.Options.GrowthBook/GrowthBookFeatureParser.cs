// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using static Excos.Options.GrowthBook.JsonConfigureOptions;

namespace Excos.Options.GrowthBook
{
    internal static class GrowthBookFeatureParser
    {
        public static IDictionary<string, string?> ConvertFeaturesToConfiguration(IDictionary<string, Models.Feature> features) =>
            JsonConfigurationFileParser.Parse(features.Select(f => (f.Key, f.Value.DefaultValue)));

        public static IEnumerable<Feature> ConvertFeaturesToExcos(IDictionary<string, Models.Feature> features)
        {
            foreach (var gbFeature in features)
            {
                var defaultValue = gbFeature.Value.DefaultValue;
                var feature = new Feature
                {
                    Name = gbFeature.Key,
                };

                var ruleIdx = 0;
                foreach (var rule in gbFeature.Value.Rules)
                {
                    var namespaceId = rule.Namespace.ValueKind == JsonValueKind.Array ? rule.Namespace[0].GetString() : null;
                    var namespaceRange = namespaceId is not null ? new Range<double>(rule.Namespace[1].GetDouble(), rule.Namespace[2].GetDouble(), RangeType.IncludeBoth) : new Range<double>();

                    var filters = new List<IFilteringCondition> { FilterParser.ParseFilters(rule.Condition) };
                    if (namespaceId is not null)
                    {
                        filters.Add(new NamespaceFilteringCondition(rule.HashAttribute, namespaceId, namespaceRange));
                    }

                    if (rule.Force.ValueKind != JsonValueKind.Undefined)
                    {
                        var allocationFilter = new AllocationFilteringCondition(
                                rule.HashAttribute,
                                rule.Seed ?? rule.Key ?? gbFeature.Key,
                                rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                                Allocation.Percentage(rule.Coverage * 100)
                            );
                        filters.Add(allocationFilter);
                        var variant = new Variant
                        {
                            Id = $"{rule.Key ?? gbFeature.Key}:Force{ruleIdx}",
                            Configuration = rule.Force,
                            Priority = ruleIdx,
                        };
                        variant.Filters = filters;
                        feature.Add(variant);
                    }
                    else if (rule.Variations.ValueKind == JsonValueKind.Array && rule.Weights != null)
                    {
                        var allocationRangeStart = 0.0;
                        for (var i = 0; i < rule.Variations.GetArrayLength(); i++)
                        {
                            var meta = rule.Meta?[i];
                            var variation = rule.Variations[i];
                            var allocation = new Range<double>(
                                Math.Max(0, allocationRangeStart * rule.Coverage),
                                Math.Min(1, (allocationRangeStart + rule.Weights[i]) * rule.Coverage),
                                RangeType.IncludeBoth);
                            allocationRangeStart += rule.Weights[i];

                            var allocationFilter = new AllocationFilteringCondition(
                                rule.HashAttribute,
                                rule.Seed ?? rule.Key ?? gbFeature.Key,
                                rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                                new Allocation(allocation)
                            );
                            var variant = new Variant
                            {
                                Id = $"{rule.Key}:{meta?.Key ?? i.ToString()}",
                                Configuration = variation,
                                Priority = ruleIdx,
                            };
                            // copy filters to allow outer collection reuse
                            variant.Filters = new List<IFilteringCondition>(filters) { allocationFilter };
                            feature.Add(variant);
                        }
                    }

                    ruleIdx++;
                }

                yield return feature;
            }
        }
    }
}
