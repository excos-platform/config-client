// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Utils;

/// <summary>
/// Utilities for converting between <see cref="JsonElement"/> and configuration formats.
/// </summary>
public static class JsonElementConversion
{
    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a configuration dictionary suitable for <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="json">The JSON element to convert.</param>
    /// <returns>A dictionary with colon-delimited keys representing the configuration hierarchy.</returns>
    public static IDictionary<string, string?> ToConfigurationDictionary(JsonElement json)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        VisitElement(json, string.Empty, result);
        return result;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a configuration dictionary with a key prefix.
    /// For non-object values, the value is stored directly under the prefix.
    /// For objects, properties are stored as prefix:property.
    /// </summary>
    /// <param name="json">The JSON element to convert.</param>
    /// <param name="keyPrefix">The key prefix (e.g., feature name).</param>
    /// <returns>A dictionary with colon-delimited keys representing the configuration hierarchy.</returns>
    public static IDictionary<string, string?> ToConfigurationDictionary(JsonElement json, string keyPrefix)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (json.ValueKind == JsonValueKind.Object)
        {
            // For objects, properties become top-level (no prefix wrapping)
            VisitElement(json, string.Empty, result);
        }
        else
        {
            // For non-objects, wrap with the prefix
            VisitElement(json, keyPrefix, result);
        }

        return result;
    }

    /// <summary>
    /// Wraps a <see cref="JsonElement"/> value in a JSON object with the specified key.
    /// Useful for creating properly structured configuration JSON.
    /// </summary>
    /// <param name="key">The key to wrap the value with.</param>
    /// <param name="value">The JSON element value to wrap.</param>
    /// <returns>A new JsonElement representing { "key": value }.</returns>
    public static JsonElement WrapInObject(string key, JsonElement value)
    {
        using var doc = JsonDocument.Parse($"{{\"{key}\": {value.GetRawText()}}}");
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Converts multiple <see cref="JsonElement"/> configurations to a merged configuration dictionary.
    /// Later elements override earlier ones for the same keys.
    /// </summary>
    /// <param name="jsonElements">The JSON elements to convert and merge.</param>
    /// <returns>A merged dictionary with colon-delimited keys.</returns>
    public static IDictionary<string, string?> ToConfigurationDictionary(IEnumerable<JsonElement> jsonElements)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in jsonElements)
        {
            VisitElement(json, string.Empty, result);
        }
        return result;
    }

    /// <summary>
    /// Converts an <see cref="IConfigurationSection"/> to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="section">The configuration section to convert.</param>
    /// <returns>A JSON element representing the configuration structure.</returns>
    public static JsonElement ToJsonElement(IConfigurationSection section)
    {
        return BuildJsonElement(section);
    }

    private static void VisitElement(JsonElement element, string prefix, Dictionary<string, string?> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                    VisitElement(property.Value, key, result);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}:{index}";
                    VisitElement(item, key, result);
                    index++;
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
                result[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
                result[prefix] = "True";
                break;

            case JsonValueKind.False:
                result[prefix] = "False";
                break;

            case JsonValueKind.Null:
                result[prefix] = null;
                break;

            case JsonValueKind.Undefined:
                // Skip undefined values
                break;
        }
    }

    private static JsonElement BuildJsonElement(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        if (children.Count == 0)
        {
            // Leaf node - return value as JSON
            var value = section.Value;
            if (value == null)
            {
                return default; // Undefined
            }
            return JsonSerializer.SerializeToElement(value);
        }

        // Check if this is an array (children have numeric keys starting from 0)
        if (IsArraySection(children))
        {
            var arrayValues = children
                .OrderBy(c => int.Parse(c.Key))
                .Select(BuildJsonElement)
                .ToList();
            return JsonSerializer.SerializeToElement(arrayValues);
        }

        // Object node
        var dict = new Dictionary<string, JsonElement>();
        foreach (var child in children)
        {
            var childElement = BuildJsonElement(child);
            if (childElement.ValueKind != JsonValueKind.Undefined)
            {
                dict[child.Key] = childElement;
            }
        }

        if (dict.Count == 0)
        {
            return JsonSerializer.SerializeToElement(new { });
        }

        return JsonSerializer.SerializeToElement(dict);
    }

    private static bool IsArraySection(List<IConfigurationSection> children)
    {
        if (children.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < children.Count; i++)
        {
            if (!int.TryParse(children[i].Key, out var index) || index != i)
            {
                // Not a sequential array starting from 0
                // Check if all keys are numeric (sparse array)
                return children.All(c => int.TryParse(c.Key, out _));
            }
        }

        return true;
    }
}
