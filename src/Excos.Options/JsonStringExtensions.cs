// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;

namespace Excos.Options;

/// <summary>
/// Extension methods for JSON string parsing and validation.
/// </summary>
internal static class JsonStringExtensions
{
    /// <summary>
    /// Parses a JSON string into a JsonElement, validating that it's valid JSON.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A JsonElement representing the parsed JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static JsonElement ParseAsJsonElement(this string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
