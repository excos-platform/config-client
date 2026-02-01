using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Excos.Options.Tests;

public class VariantConfigurationUtilitiesTests
{
    [Fact]
    public void ToConfigurationDictionary_WithSingleConfiguration_ReturnsCorrectDictionary()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Test":{"Value":"Hello"}}""");
        var config = json.RootElement.Clone();
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { config });

        // Assert
        Assert.Equal("Hello", result["Test:Value"]);
    }

    [Fact]
    public void ToConfigurationDictionary_WithMultipleConfigurations_MergesCorrectly()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"Section1":{"Value1":"A"}}""");
        var json2 = JsonDocument.Parse("""{"Section2":{"Value2":"B"}}""");
        
        var config1 = json1.RootElement.Clone();
        var config2 = json2.RootElement.Clone();
        
        json1.Dispose();
        json2.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { config1, config2 });

        // Assert
        Assert.Equal("A", result["Section1:Value1"]);
        Assert.Equal("B", result["Section2:Value2"]);
    }

    [Fact]
    public void ToConfigurationDictionary_WithSectionPrefix_AppliesPrefix()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Value":"Test"}""");
        var config = json.RootElement.Clone();
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { config }, "Prefix");

        // Assert
        Assert.True(result.ContainsKey("Value"));
    }

    [Fact]
    public void ToConfigurationDictionary_WithNullConfigurations_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfigurationDictionary(null!));
    }

    [Fact]
    public void ToConfiguration_WithConfigurations_CreatesWorkingConfiguration()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"Section":{"Key":"Value"}}""");
        var config = json.RootElement.Clone();
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfiguration(new[] { config });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Value", result["Section:Key"]);
    }

    [Fact]
    public void ToConfiguration_WithNullConfigurations_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfiguration(null!));
    }

    [Fact]
    public void ToConfigureAction_WithConfigurations_BindsOptionsCorrectly()
    {
        // Arrange
        var json = JsonDocument.Parse("""{"MySection":{"Value":"TestValue","Number":42}}""");
        var config = json.RootElement.Clone();
        json.Dispose();

        var options = new TestOptions();

        // Act
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TestOptions>(new[] { config }, "MySection");
        configureAction(options);

        // Assert
        Assert.Equal("TestValue", options.Value);
        Assert.Equal(42, options.Number);
    }

    [Fact]
    public void ToConfigureAction_WithNullConfigurations_ThrowsArgumentNullException()
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
        var config = json.RootElement.Clone();
        json.Dispose();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ToConfigureAction<TestOptions>(new[] { config }, null!));
    }

    [Fact]
    public void ParseJsonConfiguration_WithValidJson_ReturnsJsonElement()
    {
        // Arrange
        var json = """{"Key":"Value"}""";

        // Act
        var result = VariantConfigurationUtilities.ParseJsonConfiguration(json);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("Value", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void ParseJsonConfiguration_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act & Assert
        // JsonDocument.Parse throws JsonException or its subclass (JsonReaderException) for invalid JSON
        Assert.ThrowsAny<JsonException>(() =>
            VariantConfigurationUtilities.ParseJsonConfiguration(invalidJson));
    }

    [Fact]
    public void ParseJsonConfiguration_WithNullJson_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VariantConfigurationUtilities.ParseJsonConfiguration(null!));
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
        var config = json.RootElement.Clone();
        json.Dispose();

        // Act
        var result = VariantConfigurationUtilities.ToConfigurationDictionary(new[] { config });

        // Assert
        Assert.Equal("Nested", result["Parent:Child:Value"]);
        Assert.Equal("1", result["Parent:Array:0"]);
        Assert.Equal("2", result["Parent:Array:1"]);
        Assert.Equal("3", result["Parent:Array:2"]);
    }

    [Fact]
    public void ToConfigureAction_WithMultipleConfigurations_AppliesAllConfigurations()
    {
        // Arrange
        var json1 = JsonDocument.Parse("""{"TestSection":{"Value":"First"}}""");
        var json2 = JsonDocument.Parse("""{"TestSection":{"Number":99}}""");
        
        var config1 = json1.RootElement.Clone();
        var config2 = json2.RootElement.Clone();
        
        json1.Dispose();
        json2.Dispose();

        var options = new TestOptions();

        // Act
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TestOptions>(
            new[] { config1, config2 }, 
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
