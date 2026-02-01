// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Excos.Options.Tests;

public class VariantConfigurationUtilitiesTests
{
    [Fact]
    public void ToConfigurationDictionary_WithSingleVariant_ReturnsCorrectDictionary()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Test":{"Value":"Hello"}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { variant });

        // Assert
        Assert.Equal("Hello", result["Test:Value"]);
    }

    [Fact]
    public void ToConfigurationDictionary_WithMultipleVariants_MergesCorrectly()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"Section1":{"Value1":"A"}}""");
        var json2 = JsonDocument.Parse("""{"Section2":{"Value2":"B"}}""");
        
        var variant1 = new Variant
        {
            Id = "v1",
            Configuration = json1.RootElement.Clone()
        };
        var variant2 = new Variant
        {
            Id = "v2",
            Configuration = json2.RootElement.Clone()
        };
        
        json1.Dispose();
        json2.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { variant1, variant2 });

        // Assert
        Assert.Equal("A", result["Section1:Value1"]);
        Assert.Equal("B", result["Section2:Value2"]);
    }

    [Fact]
    public void ToConfigurationDictionary_WithNullVariants_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfigurationDictionary(null!));
    }

    [Fact]
    public void ToConfiguration_WithVariants_CreatesWorkingConfiguration()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Section":{"Key":"Value"}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfiguration(new[] { variant });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Value", result["Section:Key"]);
    }

    [Fact]
    public void ToConfiguration_WithNullVariants_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfiguration(null!));
    }

    [Fact]
    public void ToConfigureAction_WithVariants_BindsOptionsCorrectly()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"MySection":{"Value":"TestValue","Number":42}}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        var options = new TestOptions();

        // Act
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TestOptions>(new[] { variant }, "MySection");
        configureAction(options);

        // Assert
        Assert.Equal("TestValue", options.Value);
        Assert.Equal(42, options.Number);
    }

    [Fact]
    public void ToConfigureAction_WithNullVariants_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfigureAction<TestOptions>(null!, "Section"));
    }

    [Fact]
    public void ToConfigureAction_WithNullSection_ThrowsArgumentNullException()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Value":"Test"}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfigureAction<TestOptions>(new[] { variant }, null!));
    }

    [Fact]
    public void ToConfigureAction_WithEmptySection_BindsEntireConfiguration()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Value":"TestValue","Number":99}""");
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        var options = new TestOptions();

        // Act
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TestOptions>(new[] { variant }, "");
        configureAction(options);

        // Assert
        Assert.Equal("TestValue", options.Value);
        Assert.Equal(99, options.Number);
    }

    [Fact]
    public void ToConfigurationDictionary_WithComplexJson_ParsesNestedStructures()
    {
        // Arrange
        var json = JsonDocument.Parse("""
        {
            "Parent": {
                "Child": {
                    "Value": "Nested"
                },
                "Array": [1, 2, 3]
            }
        }
        """);
        var variant = new Variant
        {
            Id = "test",
            Configuration = json.RootElement.Clone()
        };
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { variant });

        // Assert
        Assert.Equal("Nested", result["Parent:Child:Value"]);
        Assert.Equal("1", result["Parent:Array:0"]);
        Assert.Equal("2", result["Parent:Array:1"]);
        Assert.Equal("3", result["Parent:Array:2"]);
    }

    [Fact]
    public void ToConfigureAction_WithMultipleVariants_AppliesAllConfigurations()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"TestSection":{"Value":"First"}}""");
        var json2 = JsonDocument.Parse("""{"TestSection":{"Number":99}}""");
        
        var variant1 = new Variant
        {
            Id = "v1",
            Configuration = json1.RootElement.Clone()
        };
        var variant2 = new Variant
        {
            Id = "v2",
            Configuration = json2.RootElement.Clone()
        };
        
        json1.Dispose();
        json2.Dispose();

        var options = new TestOptions();

        // Act
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TestOptions>(
            new[] { variant1, variant2 }, 
            "TestSection");
        configureAction(options);

        // Assert
        Assert.Equal("First", options.Value);
        Assert.Equal(99, options.Number);
    }

    private class TestOptions
    {
        public string Value { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}
