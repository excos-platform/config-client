// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Excos.Options.Tests;

/// <summary>
/// Tests for Variant.Configuration as JsonElement.
/// Verifies the behavioral requirements from Decision 001.
/// </summary>
public class VariantConfigurationTests
{
    #region Variant Configuration Storage

    [Fact]
    public void Variant_CanStoreObjectConfiguration()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Setting = new { Timeout = 30, Retries = 3 }
        });

        var variant = new Variant
        {
            Id = "test-variant",
            Configuration = json
        };

        Assert.Equal(JsonValueKind.Object, variant.Configuration.ValueKind);
        Assert.Equal(30, variant.Configuration.GetProperty("Setting").GetProperty("Timeout").GetInt32());
    }

    [Fact]
    public void Variant_CanStorePrimitiveConfiguration()
    {
        var json = JsonSerializer.SerializeToElement(true);

        var variant = new Variant
        {
            Id = "feature-flag",
            Configuration = json
        };

        Assert.Equal(JsonValueKind.True, variant.Configuration.ValueKind);
    }

    [Fact]
    public void Variant_ConfigurationCanBeCompared()
    {
        // One of the key benefits from Decision 001: configurations can be compared
        var json1 = JsonDocument.Parse("""{"Value": 42}""").RootElement;
        var json2 = JsonDocument.Parse("""{"Value": 42}""").RootElement;
        var json3 = JsonDocument.Parse("""{"Value": 99}""").RootElement;

        var variant1 = new Variant { Id = "v1", Configuration = json1 };
        var variant2 = new Variant { Id = "v2", Configuration = json2 };
        var variant3 = new Variant { Id = "v3", Configuration = json3 };

        // Same structure and values
        Assert.Equal(
            variant1.Configuration.GetProperty("Value").GetInt32(),
            variant2.Configuration.GetProperty("Value").GetInt32());

        // Different values
        Assert.NotEqual(
            variant1.Configuration.GetProperty("Value").GetInt32(),
            variant3.Configuration.GetProperty("Value").GetInt32());
    }

    [Fact]
    public void Variant_ConfigurationCanBeInspected()
    {
        // One of the key benefits from Decision 001: configurations can be inspected
        var json = JsonDocument.Parse("""
            {
                "Feature": {
                    "Enabled": true,
                    "MaxItems": 100,
                    "Tags": ["alpha", "beta"]
                }
            }
            """).RootElement;

        var variant = new Variant { Id = "test", Configuration = json };

        // Can enumerate properties
        var featureProps = variant.Configuration.GetProperty("Feature");
        Assert.True(featureProps.GetProperty("Enabled").GetBoolean());
        Assert.Equal(100, featureProps.GetProperty("MaxItems").GetInt32());
        Assert.Equal(2, featureProps.GetProperty("Tags").GetArrayLength());
    }

    [Fact]
    public void Variant_ConfigurationCanBeSerialized()
    {
        // One of the key benefits from Decision 001: configurations can be serialized
        var original = new Variant
        {
            Id = "serializable-variant",
            Configuration = JsonDocument.Parse("""{"Key": "Value"}""").RootElement
        };

        // Serialize the configuration
        var serialized = JsonSerializer.Serialize(original.Configuration);

        // Deserialize back
        var deserialized = JsonDocument.Parse(serialized).RootElement;

        Assert.Equal("Value", deserialized.GetProperty("Key").GetString());
    }

    #endregion

    #region Configuration Binding Patterns

    [Fact]
    public void Variant_Configuration_BindsToOptionsViaConfigurationBuilder()
    {
        var variant = new Variant
        {
            Id = "test",
            Configuration = JsonDocument.Parse("""
                {
                    "MySection": {
                        "Name": "TestName",
                        "Count": 5
                    }
                }
                """).RootElement
        };

        // This is the pattern used in FeatureEvaluation.EvaluateFeaturesAsync
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(variant.Configuration))
            .Build();

        var options = new SampleOptions();
        config.GetSection("MySection").Bind(options);

        Assert.Equal("TestName", options.Name);
        Assert.Equal(5, options.Count);
    }

    [Fact]
    public void Variant_MultipleConfigurations_ApplyInOrder()
    {
        // Simulating multiple variants being applied
        var variant1 = new Variant
        {
            Id = "base",
            Configuration = JsonDocument.Parse("""{"Section": {"A": "1", "B": "2"}}""").RootElement
        };

        var variant2 = new Variant
        {
            Id = "override",
            Configuration = JsonDocument.Parse("""{"Section": {"B": "overridden", "C": "3"}}""").RootElement
        };

        var options = new MultiPropertyOptions();

        // Apply first variant
        var config1 = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(variant1.Configuration))
            .Build();
        config1.GetSection("Section").Bind(options);

        // Apply second variant (later variants override)
        var config2 = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(variant2.Configuration))
            .Build();
        config2.GetSection("Section").Bind(options);

        Assert.Equal("1", options.A);           // From variant1, not overridden
        Assert.Equal("overridden", options.B);  // Overridden by variant2
        Assert.Equal("3", options.C);           // Added by variant2
    }

    [Fact]
    public void Variant_PrimitiveConfiguration_BindsWithFeatureNamePrefix()
    {
        // GrowthBook pattern: feature value is primitive, wrapped with feature name
        var primitiveValue = JsonSerializer.SerializeToElement(true);
        var wrapped = JsonElementConversion.WrapInObject("IsEnabled", primitiveValue);

        var variant = new Variant
        {
            Id = "feature-flag",
            Configuration = wrapped
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(variant.Configuration))
            .Build();

        var options = new FeatureFlagOptions();
        config.Bind(options);

        Assert.True(options.IsEnabled);
    }

    private class SampleOptions
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    private class MultiPropertyOptions
    {
        public string? A { get; set; }
        public string? B { get; set; }
        public string? C { get; set; }
    }

    private class FeatureFlagOptions
    {
        public bool IsEnabled { get; set; }
    }

    #endregion

    #region Priority Handling

    [Fact]
    public void Variant_NullPriority_TreatedAsLowestPriority()
    {
        var variant1 = new Variant { Id = "v1", Priority = 1 };
        var variant2 = new Variant { Id = "v2", Priority = null };
        var variant3 = new Variant { Id = "v3", Priority = 100 };

        var variants = new[] { variant2, variant1, variant3 };

        // Sort by priority (null should be last)
        var sorted = variants
            .OrderBy(v => v.Priority ?? int.MaxValue)
            .ToList();

        Assert.Equal("v1", sorted[0].Id);    // Priority 1 (highest)
        Assert.Equal("v3", sorted[1].Id);    // Priority 100
        Assert.Equal("v2", sorted[2].Id);    // Priority null (lowest)
    }

    #endregion
}
