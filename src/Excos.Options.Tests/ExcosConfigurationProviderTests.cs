// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Contextual;
using Excos.Options.Providers.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests;

/// <summary>
/// Tests for the ExcosConfigurationProvider via the public AddExcosConfiguration extension methods.
/// </summary>
public class ExcosConfigurationProviderTests
{
    #region Basic Functionality

    [Fact]
    public void AddExcosConfiguration_WithSingleVariant_PopulatesConfiguration()
    {
        // Arrange
        var feature = CreateFeature("TestFeature",
            new Variant
            {
                Id = "TestVariant",
                Configuration = JsonDocument.Parse("""{"Section":{"Key":"Value"}}""").RootElement,
                Filters = []
            });

        var featureProvider = new TestFeatureProvider(feature);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert
        Assert.Equal("Value", config["Section:Key"]);
    }

    [Fact]
    public void AddExcosConfiguration_WithMultipleFeatures_CombinesConfiguration()
    {
        // Arrange
        var feature1 = CreateFeature("Feature1",
            new Variant
            {
                Id = "V1",
                Configuration = JsonDocument.Parse("""{"Section1":{"Key":"Value1"}}""").RootElement,
                Filters = []
            });

        var feature2 = CreateFeature("Feature2",
            new Variant
            {
                Id = "V2",
                Configuration = JsonDocument.Parse("""{"Section2":{"Key":"Value2"}}""").RootElement,
                Filters = []
            });

        var featureProvider = new TestFeatureProvider(feature1, feature2);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert
        Assert.Equal("Value1", config["Section1:Key"]);
        Assert.Equal("Value2", config["Section2:Key"]);
    }

    [Fact]
    public void AddExcosConfiguration_WithNoMatchingVariants_ConfigurationEmpty()
    {
        // Arrange - variant has filter that won't match empty context
        var feature = CreateFeature("TestFeature",
            new Variant
            {
                Id = "TestVariant",
                Configuration = JsonDocument.Parse("""{"Key":"Value"}""").RootElement,
                Filters = [new TestFilter(false)]
            });

        var featureProvider = new TestFeatureProvider(feature);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert
        Assert.Null(config["Key"]);
    }

    [Fact]
    public void AddExcosConfiguration_WithEmptyFeatureList_ConfigurationEmpty()
    {
        // Arrange
        var featureProvider = new TestFeatureProvider();

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert
        Assert.Null(config["Key"]);
    }

    #endregion

    #region Priority Ordering

    [Fact]
    public void AddExcosConfiguration_WithMultipleVariants_SelectsLowestPriority()
    {
        // Arrange - lower priority value = higher priority (selected first)
        var feature = CreateFeature("TestFeature",
            new Variant
            {
                Id = "HighPriority",
                Priority = 1,
                Configuration = JsonDocument.Parse("""{"Key":"High"}""").RootElement,
                Filters = []
            },
            new Variant
            {
                Id = "LowPriority",
                Priority = 10,
                Configuration = JsonDocument.Parse("""{"Key":"Low"}""").RootElement,
                Filters = []
            });

        var featureProvider = new TestFeatureProvider(feature);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert - should pick the variant with lowest priority number (1)
        Assert.Equal("High", config["Key"]);
    }

    [Fact]
    public void AddExcosConfiguration_WithNullPriority_TreatedAsLowestPriority()
    {
        // Arrange - null priority should be considered last
        var feature = CreateFeature("TestFeature",
            new Variant
            {
                Id = "NullPriority",
                Priority = null,
                Configuration = JsonDocument.Parse("""{"Key":"Null"}""").RootElement,
                Filters = []
            },
            new Variant
            {
                Id = "ExplicitPriority",
                Priority = 100,
                Configuration = JsonDocument.Parse("""{"Key":"Explicit"}""").RootElement,
                Filters = []
            });

        var featureProvider = new TestFeatureProvider(feature);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(featureProvider, new Dictionary<string, string>())
            .Build();

        // Assert - should pick variant with explicit priority over null
        Assert.Equal("Explicit", config["Key"]);
    }

    #endregion

    #region Multiple Providers

    [Fact]
    public void AddExcosConfiguration_WithMultipleProviders_CombinesResults()
    {
        // Arrange
        var feature1 = CreateFeature("Feature1",
            new Variant
            {
                Id = "V1",
                Configuration = JsonDocument.Parse("""{"Provider1":{"Key":"Value1"}}""").RootElement,
                Filters = []
            });

        var feature2 = CreateFeature("Feature2",
            new Variant
            {
                Id = "V2",
                Configuration = JsonDocument.Parse("""{"Provider2":{"Key":"Value2"}}""").RootElement,
                Filters = []
            });

        var provider1 = new TestFeatureProvider(feature1);
        var provider2 = new TestFeatureProvider(feature2);

        // Act
        var config = new ConfigurationBuilder()
            .AddExcosConfiguration(
                new IFeatureProvider[] { provider1, provider2 },
                new DictionaryOptionsContext(new Dictionary<string, string>()))
            .Build();

        // Assert
        Assert.Equal("Value1", config["Provider1:Key"]);
        Assert.Equal("Value2", config["Provider2:Key"]);
    }

    #endregion

    #region Test Helpers

    private static Feature CreateFeature(string name, params Variant[] variants)
    {
        var feature = new Feature { Name = name };
        foreach (var variant in variants)
        {
            feature.Add(variant);
        }
        return feature;
    }

    private class TestFeatureProvider : IFeatureProvider
    {
        private readonly Feature[] _features;

        public TestFeatureProvider(params Feature[] features)
        {
            _features = features;
        }

        public ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<IEnumerable<Feature>>(_features);
        }
    }

    private class TestFilter : IFilteringCondition
    {
        private readonly bool _result;

        public TestFilter(bool result)
        {
            _result = result;
        }

        public bool IsSatisfiedBy<TContext>(TContext context)
            where TContext : IOptionsContext
            => _result;
    }

    #endregion
}
