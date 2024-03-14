// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
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
                    ProviderName = nameof(GrowthBook),
                    Salt = gbFeature.Key,
                };

                var ruleIdx = 0;
                foreach (var rule in gbFeature.Value.Rules)
                {
                    var namespaceId = rule.Namespace.ValueKind == JsonValueKind.Array ? rule.Namespace[0].GetString() : null;
                    var namespaceRange = namespaceId is not null ? new Range<double>(rule.Namespace[1].GetDouble(), rule.Namespace[2].GetDouble(), RangeType.IncludeBoth) : (Range<double>?)null;

                    var filters = FilterParser.ParseFilters(rule.Condition);
                    if (namespaceId is not null)
                    {
                        filters[rule.HashAttribute] = filters.TryGetValue(rule.HashAttribute, out var filter)
                            ? new NamespaceInclusiveFilter(namespaceId, namespaceRange!.Value, filter)
                            : new NamespaceInclusiveFilter(namespaceId, namespaceRange!.Value, null);
                    }

                    if (rule.Force.ValueKind != JsonValueKind.Undefined)
                    {
                        var variant = new Variant
                        {
                            Id = $":{ruleIdx}",
                            Allocation = Allocation.Percentage(rule.Coverage * 100),
                            Configuration = new JsonConfigureOptions(gbFeature.Key, rule.Force),
                            Priority = ruleIdx,
                            AllocationUnit = rule.HashAttribute,
                            AllocationSalt = rule.Seed ?? rule.Key,
                            AllocationHash = rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                        };
                        variant.Filters.AddRange(filters.Select(kvp => new Filter { PropertyName = kvp.Key, Conditions = { kvp.Value } }));
                        feature.Variants.Add(variant);
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

                            var variant = new Variant
                            {
                                Id = $"{rule.Key}:{meta?.Key ?? i.ToString()}",
                                Allocation = new Allocation(allocation),
                                Configuration = new JsonConfigureOptions(gbFeature.Key, variation),
                                Priority = ruleIdx,
                                AllocationUnit = rule.HashAttribute,
                                AllocationSalt = rule.Seed ?? rule.Key,
                                AllocationHash = rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                            };
                            variant.Filters.AddRange(filters.Select(kvp => new Filter { PropertyName = kvp.Key, Conditions = { kvp.Value } }));
                            feature.Variants.Add(variant);
                        }
                    }

                    ruleIdx++;
                }

                yield return feature;
            }
        }
    }
}
