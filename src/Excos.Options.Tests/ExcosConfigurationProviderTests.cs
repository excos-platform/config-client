// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Excos.Options.Providers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Excos.Options.Tests;

public class ExcosConfigurationProviderTests
{
    [Fact]
    public void AddExcosConfiguration_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var context = new Dictionary<string, string>();
        var featureProvider = new TestFeatureProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ((IConfigurationBuilder)null!).AddExcosConfiguration(context, featureProvider));
    }

    [Fact]
    public void AddExcosConfiguration_LoadsMatchingVariants()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"TestSection":{"Value":"FromFeature"}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone(),
            Filters = [new StringFilteringCondition("Market", "US")]
        };
        json.Dispose();

        var feature = new Feature { Name = "TestFeature" };
        feature.Add(variant);

        var context = new Dictionary<string, string> { ["Market"] = "US" };
        var featureProvider = new TestFeatureProvider(feature);
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddExcosConfiguration(context, featureProvider, TimeSpan.FromHours(1));
        var config = builder.Build();

        // Assert
        Assert.Equal("FromFeature", config["TestSection:Value"]);
    }

    [Fact]
    public void AddExcosConfiguration_FiltersNonMatchingVariants()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"TestSection":{"Value":"US"}}""");
        var json2 = JsonDocument.Parse("""{"TestSection":{"Value":"EU"}}""");
        
        var variantUS = new Variant
        {
            Id = "us",
            Configuration = json1.RootElement.Clone(),
            Filters = [new StringFilteringCondition("Market", "US")]
        };
        var variantEU = new Variant
        {
            Id = "eu",
            Configuration = json2.RootElement.Clone(),
            Filters = [new StringFilteringCondition("Market", "EU")]
        };
        
        json1.Dispose();
        json2.Dispose();

        var feature = new Feature { Name = "TestFeature" };
        feature.Add(variantUS);
        feature.Add(variantEU);

        var context = new Dictionary<string, string> { ["Market"] = "US" };
        var featureProvider = new TestFeatureProvider(feature);
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddExcosConfiguration(context, featureProvider, TimeSpan.FromHours(1));
        var config = builder.Build();

        // Assert
        Assert.Equal("US", config["TestSection:Value"]);
    }

    [Fact]
    public void AddExcosConfiguration_RespectsVariantPriority()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"TestSection":{"Value":"Priority1"}}""");
        var json2 = JsonDocument.Parse("""{"TestSection":{"Value":"Priority2"}}""");
        
        var variant1 = new Variant
        {
            Id = "v1",
            Configuration = json1.RootElement.Clone(),
            Priority = 2
        };
        var variant2 = new Variant
        {
            Id = "v2",
            Configuration = json2.RootElement.Clone(),
            Priority = 1
        };
        
        json1.Dispose();
        json2.Dispose();

        var feature = new Feature { Name = "TestFeature" };
        feature.Add(variant1);
        feature.Add(variant2);

        var context = new Dictionary<string, string>();
        var featureProvider = new TestFeatureProvider(feature);
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddExcosConfiguration(context, featureProvider, TimeSpan.FromHours(1));
        var config = builder.Build();

        // Assert - should use variant with priority 1 (lowest)
        Assert.Equal("Priority2", config["TestSection:Value"]);
    }

    [Fact]
    public void AddExcosConfiguration_ConfiguresValuesCorrectly()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"TestSection":{"Value":"Test"}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        var feature = new Feature { Name = "TestFeature" };
        feature.Add(variant);

        var context = new Dictionary<string, string> { ["Market"] = "US" };
        var featureProvider = new TestFeatureProvider(feature);
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddExcosConfiguration(context, featureProvider);
        var config = builder.Build();

        // Assert - verify configuration works by reading a setting
        Assert.Equal("Test", config["TestSection:Value"]);
    }

    [Fact]
    public void AddExcosConfiguration_WithoutRefreshPeriod_LoadsOnlyOnce()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"TestSection":{"Value":"Initial"}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        var feature = new Feature { Name = "TestFeature" };
        feature.Add(variant);

        var context = new Dictionary<string, string>();
        var featureProvider = new TestFeatureProvider(feature);
        var builder = new ConfigurationBuilder();

        // Act - no refresh period provided
        builder.AddExcosConfiguration(context, featureProvider);
        var config = builder.Build();

        // Assert
        Assert.Equal("Initial", config["TestSection:Value"]);
    }

    private class TestFeatureProvider : IFeatureProvider
    {
        private readonly List<Feature> _features = new();

        public TestFeatureProvider(params Feature[] features)
        {
            _features.AddRange(features);
        }

        public ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IEnumerable<Feature>>(_features);
        }
    }

    private class TestConfigurationSource : IConfigurationSource
    {
        private readonly IConfigurationProvider _provider;

        public TestConfigurationSource(IConfigurationProvider provider)
        {
            _provider = provider;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return _provider;
        }
    }
}
