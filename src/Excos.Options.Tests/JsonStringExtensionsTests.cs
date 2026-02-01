// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Xunit;

namespace Excos.Options.Tests;

public class JsonStringExtensionsTests
{
    [Fact]
    public void ParseAsJsonElement_WithValidJson_ReturnsJsonElement()
    {
        // Arrange
        var json = """{"Key":"Value"}""";

        // Act
        var result = json.ParseAsJsonElement();

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("Value", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void ParseAsJsonElement_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => invalidJson.ParseAsJsonElement());
    }

    [Fact]
    public void ParseAsJsonElement_WithNullJson_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ((string)null!).ParseAsJsonElement());
    }
}
