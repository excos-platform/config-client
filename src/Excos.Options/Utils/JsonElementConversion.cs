// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Utils;

/// <summary>
/// Utilities for converting between <see cref="JsonElement"/> and configuration formats.
/// </summary>
internal static class JsonElementConversion
{
    /// <summary>
    /// Converts multiple variant configurations to a merged configuration dictionary.
    /// Later variants override earlier ones for the same keys.
    /// </summary>
    /// <param name="variants">The variants whose configurations should be merged.</param>
    /// <returns>A merged dictionary with colon-delimited keys.</returns>
    public static Dictionary<string, string?> ToConfigurationDictionary(IEnumerable<Variant> variants)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants)
        {
            VisitElement(variant.Configuration, string.Empty, result);
        }
        return result;
    }

    /// <summary>
    /// Merges multiple variant configurations into a single <see cref="IConfiguration"/> instance.
    /// Later variants override earlier ones for the same keys.
    /// </summary>
    /// <param name="variants">The variants whose configurations should be merged.</param>
    /// <returns>An <see cref="IConfiguration"/> containing the merged configuration data.</returns>
    public static IConfiguration MergeVariantConfigurations(IEnumerable<Variant> variants)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(ToConfigurationDictionary(variants))
            .Build();
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
        // Use JsonSerializer to properly escape the key to prevent JSON injection
        var escapedKey = JsonSerializer.Serialize(key);
        using var doc = JsonDocument.Parse($"{{{escapedKey}: {value.GetRawText()}}}");
        return doc.RootElement.Clone();
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
            // int.Parse is safe here because IsArraySection validates all keys are numeric
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
