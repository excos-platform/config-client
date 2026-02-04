// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Excos.Options.GrowthBook.Tests;

public class Tests
{
    private const string Payload =
    """
    {
        "status": 200,
        "features": {
            "newlabel": {
                "defaultValue": {
                    "MyOptions": {
                        "Label": "Old"
                    }
                },
                "rules": [
                    {
                        "condition": {
                            "country": {
                                "$in": [
                                    "US",
                                    "UK"
                                ]
                            }
                        },
                        "coverage": 0.2,
                        "hashAttribute": "id",
                        "namespace": [
                            "anonymous",
                            0.2,
                            0.8
                        ],
                        "seed": "label",
                        "hashVersion": 2,
                        "variations": [
                            {
                                "MyOptions": {
                                    "Label": "Old"
                                }
                            },
                            {
                                "MyOptions": {
                                    "Label": "New"
                                }
                            }
                        ],
                        "weights": [
                            0.5,
                            0.5
                        ],
                        "key": "label",
                        "meta": [
                            {
                                "key": "0",
                                "name": "Old"
                            },
                            {
                                "key": "1",
                                "name": "New"
                            }
                        ],
                        "phase": "0",
                        "name": "LabelComparison"
                    }
                ]
            },
            "gbdemo-checkout-layout": {
                "defaultValue": "current",
                "rules": [
                    {
                        "condition": {
                            "is_employee": true
                        },
                        "force": "dev"
                    },
                    {
                        "coverage": 1,
                        "seed": "gbdemo-checkout-layout",
                        "hashVersion": 2,
                        "variations": [
                            "current",
                            "dev-compact",
                            "dev"
                        ],
                        "weights": [
                            0.3334,
                            0.3333,
                            0.3333
                        ],
                        "key": "gbdemo-checkout-layout",
                        "meta": [
                            {
                                "key": "0",
                                "name": "Current"
                            },
                            {
                                "key": "1",
                                "name": "Dev-Compact"
                            },
                            {
                                "key": "2",
                                "name": "Dev"
                            }
                        ],
                        "phase": "0",
                        "name": "gbdemo-checkout-layout"
                    },
                    {
                        "condition": {
                            "employee": true
                        },
                        "force": "dev",
                        "coverage": 0.25,
                        "hashAttribute": "id"
                    }
                ]
            },
            "filtered": {
                "defaultValue": {},
                "rules": [
                    {
                        "condition": {
                            "id": {
                                "$exists": true
                            },
                            "browser": {
                                "$ne": "1",
                                "$eq": "3"
                            },
                            "deviceId": {
                                "$gt": "5"
                            },
                            "company": {
                                "$regex": "a.*c"
                            },
                            "country": {
                                "$exists": false
                            },
                            "Tags": {
                                "$size": 0,
                                "$elemMatch": {
                                    "$eq": "A"
                                }
                            },
                            "version": {
                                "$veq": "1.2.3"
                            }
                        },
                        "force": {}
                    }
                ]
            }
        },
        "dateUpdated": "2024-01-02T21:22:10.743Z"
    }
    """;

    [Fact]
    public async Task FeaturesAreParsed()
    {
        var host = BuildHost(new GrowthBookOptions());
        var provider = (GrowthBookFeatureProvider)host.Services.GetRequiredService<IFeatureProvider>();

        var features = (await provider.GetFeaturesAsync(default)).ToList();

        Assert.Equal(3, features.Count);

        Assert.Equal("newlabel", features[0].Name);
        Assert.Equal(3, features[0].Count); // 2 rule variants + 1 default variant
        Assert.Equal("label:0", features[0][0].Id);
        Assert.Equal("label:1", features[0][1].Id);
        Assert.Equal("newlabel:default", features[0][2].Id);
        Assert.Equal(3, features[0][0].Filters.Count()); // attribute filter + allocation + namespace

        Assert.Equal("gbdemo-checkout-layout", features[1].Name);

        Assert.Equal("filtered", features[2].Name);
    }

    [Fact]
    public async Task VariantConfiguration_IsJsonElement()
    {
        // Verifies that primitive values get wrapped with feature name as key
        var host = BuildHost(new GrowthBookOptions());
        var provider = (GrowthBookFeatureProvider)host.Services.GetRequiredService<IFeatureProvider>();

        var features = (await provider.GetFeaturesAsync(default)).ToList();

        // String feature (gbdemo-checkout-layout with string variations)
        // Primitive values get wrapped with feature name as key
        var stringVariant = features[1][0];
        Assert.True(stringVariant.Configuration.TryGetProperty("gbdemo-checkout-layout", out _));
    }

    [Fact]
    public void ConfigurationIsSetUp()
    {
        // Use the new AddExcosGrowthBookConfiguration extension for configuration scenarios
        var config = new ConfigurationBuilder()
            .AddExcosGrowthBookConfiguration(options =>
            {
                options.ClientKey = "test-key";
                options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
            })
            .Build();

        // Default values are now included as variants and loaded into configuration
        Assert.Equal("Old", config.GetValue<string>("MyOptions:Label"));
        Assert.Equal("current", config.GetValue<string>("gbdemo-checkout-layout"));
    }

    // Exceptions:
    // (name: "equals object - fail extra property",
    //  condition: { "tags": { "hello": "world" } },
    //  attributes: { "tags": { "hello": "world", "yes": "please" } },
    //  expected: False) - we will only enforce properties specified, you can add extra properties without failing existing filters
    // (name: "$gt/$lt strings - fail uppercase",
    //  condition: { "word": { "$gt": "alphabet", "$lt": "zebra" } },
    //  attributes: { "word": "AZL" },
    //  expected: False) - we do case insensitive string comparison
    // (name: "missing attribute with comparison operators",
    //  condition: { "age": { "$gt": -10, "$lt": 10, "$gte": -9, "$lte": 9, "$ne": 10 } },
    //  attributes: {},
    //  expected: True) - I don't know why this was supposed to pass
    [Theory]
    [MemberData(nameof(Cases.EvalConditions), MemberType = typeof(Cases))]
    public void EvalConditions_Test(string name, JsonElement condition, JsonElement attributes, bool expected)
    {
        _ = name; // get compiler off my back
       var filter = FilterParser.ParseCondition(condition);
       Assert.Equal(expected, filter.IsSatisfied(attributes));
    }

    [Theory]
    [MemberData(nameof(Cases.Hash), MemberType = typeof(Cases))]
    public void Hash_Test(string seed, string identifier, int version, double? result)
    {
        var algorithm = new GrowthBookHash(version);
        var hash = algorithm.GetAllocationSpot(seed, identifier);
        Assert.Equal(result, hash == -1 ? null : hash);
    }

    // Exceptions:
    // ["gt", "1.2.3-5-foo", "1.2.3-5-Foo", true] - because I do case insensitive compare
    // ["gt, "1.2.3-r100", "1.2.3-R2", true], - because I do case insensitive compare
    [Theory]
    [MemberData(nameof(Cases.VersionCompare), MemberType = typeof(Cases))]
    public void VersionCompare_Test(string op, string left, string right, bool match)
    {
        var comparisonType = op switch
        {
            "eq" => ComparisonType.Equal,
            "ne" => ComparisonType.NotEqual,
            "lt" => ComparisonType.LessThan,
            "lte" => ComparisonType.LessThanOrEqual,
            "gt" => ComparisonType.GreaterThan,
            "gte" => ComparisonType.GreaterThanOrEqual,
            _ => throw new Exception(op)
        };
        var algorithm = new ComparisonVersionStringFilter(comparisonType, right);
        Assert.Equal(match, algorithm.IsSatisfied(JsonSerializer.SerializeToElement(left)));
    }

    private IHost BuildHost(GrowthBookOptions options)
    {
        // For testing, we inject the mock HTTP client factory via options
        options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));

        var builder = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(opts =>
            {
                opts.ClientKey = string.IsNullOrEmpty(options.ClientKey) ? "test-key" : options.ClientKey;
                opts.ApiHost = options.ApiHost;
                opts.CacheDuration = options.CacheDuration;
                opts.RequestFeaturesOnInitialization = options.RequestFeaturesOnInitialization;
                opts.HttpClientFactory = options.HttpClientFactory;
            });

        return builder.Build();
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly DelegatingHandler _handler;
        public MockHttpClientFactory(DelegatingHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler);
        }
    }

    private class MockHandler : DelegatingHandler
    {
        private readonly string _content;
        public MockHandler(string content) => _content = content;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent(_content);
            return Task.FromResult(response);
        }
    }
}
