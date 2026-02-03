// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Excos.Options.Tests;

public class JsonConfigurationConversionTests
{
    #region JsonElement to Configuration Dictionary

    [Fact]
    public void ToConfigurationDictionary_SimpleObject_ReturnsFlattened()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Section = new
            {
                Key = "Value"
            }
        });

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.Equal("Value", result["Section:Key"]);
    }

    [Fact]
    public void ToConfigurationDictionary_NestedObject_ReturnsFlattened()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Key = "DeepValue"
                }
            }
        });

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.Equal("DeepValue", result["Level1:Level2:Key"]);
    }

    [Fact]
    public void ToConfigurationDictionary_Array_ReturnsIndexedKeys()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Items = new[] { "A", "B", "C" }
        });

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.Equal("A", result["Items:0"]);
        Assert.Equal("B", result["Items:1"]);
        Assert.Equal("C", result["Items:2"]);
    }

    [Fact]
    public void ToConfigurationDictionary_PrimitiveTypes_ReturnsStringValues()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            StringVal = "text",
            IntVal = 42,
            BoolVal = true,
            DoubleVal = 3.14
        });

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.Equal("text", result["StringVal"]);
        Assert.Equal("42", result["IntVal"]);
        Assert.Equal("True", result["BoolVal"]);
        Assert.Equal("3.14", result["DoubleVal"]);
    }

    [Fact]
    public void ToConfigurationDictionary_NullValue_ReturnsNullEntry()
    {
        var json = JsonDocument.Parse("""{"Key": null}""").RootElement;

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.True(result.ContainsKey("Key"));
        Assert.Null(result["Key"]);
    }

    [Fact]
    public void ToConfigurationDictionary_EmptyObject_ReturnsEmptyDictionary()
    {
        var json = JsonSerializer.SerializeToElement(new { });

        var result = JsonElementConversion.ToConfigurationDictionary(json);

        Assert.Empty(result);
    }

    [Fact]
    public void ToConfigurationDictionary_MultipleVariants_MergesInOrder()
    {
        var variant1 = JsonSerializer.SerializeToElement(new { Section = new { A = "1", B = "2" } });
        var variant2 = JsonSerializer.SerializeToElement(new { Section = new { B = "Override", C = "3" } });

        var result = JsonElementConversion.ToConfigurationDictionary(new[] { variant1, variant2 });

        Assert.Equal("1", result["Section:A"]);
        Assert.Equal("Override", result["Section:B"]); // Overridden by variant2
        Assert.Equal("3", result["Section:C"]);
    }

    #endregion

    #region IConfigurationSection to JsonElement

    [Fact]
    public void ToJsonElement_SimpleSection_ReturnsJsonObject()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Section:Key"] = "Value"
            })
            .Build();

        var json = JsonElementConversion.ToJsonElement(config.GetSection("Section"));

        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.Equal("Value", json.GetProperty("Key").GetString());
    }

    [Fact]
    public void ToJsonElement_NestedSection_ReturnsNestedJsonObject()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Root:Level1:Level2:Key"] = "DeepValue"
            })
            .Build();

        var json = JsonElementConversion.ToJsonElement(config.GetSection("Root"));

        Assert.Equal("DeepValue", json.GetProperty("Level1").GetProperty("Level2").GetProperty("Key").GetString());
    }

    [Fact]
    public void ToJsonElement_ArraySection_ReturnsJsonArray()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Items:0"] = "A",
                ["Items:1"] = "B",
                ["Items:2"] = "C"
            })
            .Build();

        var json = JsonElementConversion.ToJsonElement(config.GetSection("Items"));

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(3, json.GetArrayLength());
        Assert.Equal("A", json[0].GetString());
        Assert.Equal("B", json[1].GetString());
        Assert.Equal("C", json[2].GetString());
    }

    [Fact]
    public void ToJsonElement_EmptySection_ReturnsEmptyObject()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var json = JsonElementConversion.ToJsonElement(config.GetSection("NonExistent"));

        // Empty section returns empty object or undefined - verify behavior
        Assert.True(json.ValueKind == JsonValueKind.Object || json.ValueKind == JsonValueKind.Undefined);
    }

    #endregion

    #region WrapInObject

    [Fact]
    public void WrapInObject_PrimitiveValue_ReturnsWrappedObject()
    {
        var value = JsonSerializer.SerializeToElement("test-value");

        var result = JsonElementConversion.WrapInObject("FeatureName", value);

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("test-value", result.GetProperty("FeatureName").GetString());
    }

    [Fact]
    public void WrapInObject_BoolValue_ReturnsWrappedObject()
    {
        var value = JsonSerializer.SerializeToElement(true);

        var result = JsonElementConversion.WrapInObject("IsEnabled", value);

        Assert.True(result.GetProperty("IsEnabled").GetBoolean());
    }

    [Fact]
    public void WrapInObject_NumberValue_ReturnsWrappedObject()
    {
        var value = JsonSerializer.SerializeToElement(42);

        var result = JsonElementConversion.WrapInObject("Count", value);

        Assert.Equal(42, result.GetProperty("Count").GetInt32());
    }

    [Fact]
    public void WrapInObject_ObjectValue_ReturnsNestedObject()
    {
        var value = JsonSerializer.SerializeToElement(new { Inner = "data" });

        var result = JsonElementConversion.WrapInObject("Outer", value);

        Assert.Equal("data", result.GetProperty("Outer").GetProperty("Inner").GetString());
    }

    #endregion

    #region Prefixed Configuration Dictionary

    [Fact]
    public void ToConfigurationDictionary_WithPrefix_ObjectValue_NoPrefix()
    {
        // Objects should NOT get prefixed (their properties become top-level)
        var json = JsonSerializer.SerializeToElement(new { Key = "Value" });

        var result = JsonElementConversion.ToConfigurationDictionary(json, "FeatureName");

        Assert.Equal("Value", result["Key"]);
        Assert.False(result.ContainsKey("FeatureName:Key"));
    }

    [Fact]
    public void ToConfigurationDictionary_WithPrefix_PrimitiveValue_GetsPrefix()
    {
        // Non-objects should get wrapped with the prefix
        var json = JsonSerializer.SerializeToElement("simple-value");

        var result = JsonElementConversion.ToConfigurationDictionary(json, "FeatureName");

        Assert.Equal("simple-value", result["FeatureName"]);
    }

    [Fact]
    public void ToConfigurationDictionary_WithPrefix_BoolValue_GetsPrefix()
    {
        var json = JsonSerializer.SerializeToElement(true);

        var result = JsonElementConversion.ToConfigurationDictionary(json, "IsEnabled");

        Assert.Equal("True", result["IsEnabled"]);
    }

    #endregion

    #region End-to-End Options Binding

    [Fact]
    public void JsonElement_BindsToOptions_ViaConfigurationBuilder()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Section = new
            {
                StringProperty = "test",
                IntProperty = 42,
                BoolProperty = true
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(json))
            .Build();

        var options = new TestOptions();
        config.GetSection("Section").Bind(options);

        Assert.Equal("test", options.StringProperty);
        Assert.Equal(42, options.IntProperty);
        Assert.True(options.BoolProperty);
    }

    [Fact]
    public void JsonElement_WithNestedObjects_BindsToOptions()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Parent = new
            {
                Child = new
                {
                    Value = "nested"
                }
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(json))
            .Build();

        var options = new NestedOptions();
        config.GetSection("Parent").Bind(options);

        Assert.Equal("nested", options.Child?.Value);
    }

    [Fact]
    public void JsonElement_WithArrays_BindsToOptions()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            Settings = new
            {
                Tags = new[] { "tag1", "tag2", "tag3" }
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(json))
            .Build();

        var options = new OptionsWithArray();
        config.GetSection("Settings").Bind(options);

        Assert.Equal(3, options.Tags?.Length);
        Assert.Equal("tag1", options.Tags?[0]);
        Assert.Equal("tag2", options.Tags?[1]);
        Assert.Equal("tag3", options.Tags?[2]);
    }

    private class TestOptions
    {
        public string? StringProperty { get; set; }
        public int IntProperty { get; set; }
        public bool BoolProperty { get; set; }
    }

    private class NestedOptions
    {
        public ChildOptions? Child { get; set; }
    }

    private class ChildOptions
    {
        public string? Value { get; set; }
    }

    private class OptionsWithArray
    {
        public string[]? Tags { get; set; }
    }

    #endregion
}
