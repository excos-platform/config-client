// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Configuration;

namespace Excos.Options;

/// <summary>
/// Utilities for converting variant configurations to various formats.
/// </summary>
public static class VariantConfigurationUtilities
{
    /// <summary>
    /// Converts an enumerable of variant JsonElement configurations into a configuration dictionary.
    /// </summary>
    /// <param name="configurations">The JsonElement configurations to convert.</param>
    /// <param name="sectionPrefix">Optional prefix for configuration keys.</param>
    /// <returns>A dictionary suitable for use with the configuration framework.</returns>
    public static IDictionary<string, string?> ToConfigurationDictionary(
        IEnumerable<JsonElement> configurations,
        string? sectionPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var inputs = configurations.Select(config =>
        {
            // Each variant should have a feature name as context
            // We'll use empty string as default if no prefix
            var prefix = sectionPrefix ?? string.Empty;
            return (prefix, config);
        });

        return JsonConfigurationFileParser.Parse(inputs);
    }

    /// <summary>
    /// Converts an enumerable of JsonElement configurations into an IConfiguration.
    /// </summary>
    /// <param name="configurations">The JsonElement configurations to convert.</param>
    /// <param name="sectionPrefix">Optional prefix for configuration keys.</param>
    /// <returns>An IConfiguration built from the configurations.</returns>
    public static IConfiguration ToConfiguration(
        IEnumerable<JsonElement> configurations,
        string? sectionPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var dictionary = ToConfigurationDictionary(configurations, sectionPrefix);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary)
            .Build();
    }

    /// <summary>
    /// Creates a configure action that binds JsonElement configurations to an options object.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <param name="configurations">The JsonElement configurations to convert.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>An action that configures the options object.</returns>
    public static Action<TOptions> ToConfigureAction<TOptions>(
        IEnumerable<JsonElement> configurations,
        string section)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentNullException.ThrowIfNull(section);

        // Materialize the configurations to avoid multiple enumeration
        var configList = configurations.ToList();
        
        return options =>
        {
            var configuration = ToConfiguration(configList);
            configuration.GetSection(section).Bind(options);
        };
    }

    /// <summary>
    /// Parses a JSON string into a JsonElement, validating that it's valid JSON.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A JsonElement representing the parsed JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static JsonElement ParseJsonConfiguration(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Converts an IConfiguration section to a JsonElement.
    /// </summary>
    /// <param name="configuration">The configuration section to convert.</param>
    /// <returns>A JsonElement representing the configuration data.</returns>
    public static JsonElement ConvertConfigurationToJson(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var dict = new Dictionary<string, object?>();
        BuildDictionary(configuration, dict, string.Empty);

        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void BuildDictionary(IConfiguration configuration, Dictionary<string, object?> dict, string prefix)
    {
        foreach (var child in configuration.GetChildren())
        {
            var key = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
            
            if (child.GetChildren().Any())
            {
                // Has children, recurse
                var childDict = new Dictionary<string, object?>();
                BuildDictionary(child, childDict, string.Empty);
                dict[child.Key] = childDict;
            }
            else
            {
                // Leaf node
                dict[child.Key] = child.Value;
            }
        }
    }

    /// <summary>
    /// Copied from Microsoft.Extensions.Configuration.Json (MIT License)
    /// Added some modifications.
    /// </summary>
    internal sealed class JsonConfigurationFileParser
    {
        private JsonConfigurationFileParser() { }

        private readonly Dictionary<string, string?> _data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _paths = new Stack<string>();

        public static IDictionary<string, string?> Parse(string featureName, JsonElement input)
        {
            var parser = new JsonConfigurationFileParser();
            parser.VisitRootElement(featureName, input);
            return parser._data;
        }

        public static IDictionary<string, string?> Parse(IEnumerable<(string featureName, JsonElement value)> inputs)
        {
            var parser = new JsonConfigurationFileParser();
            foreach (var input in inputs)
            {
                parser.VisitRootElement(input.featureName, input.value);
            }
            return parser._data;
        }

        private void VisitRootElement(string featureName, JsonElement root)
        {
            // We assume that an object at the root represents a JSON configuration, not just a single value.
            if (root.ValueKind == JsonValueKind.Object)
            {
                VisitObjectElement(root);
            }
            else
            {
                EnterContext(featureName);
                VisitValue(root);
                ExitContext();
            }

        }

        private void VisitObjectElement(JsonElement element)
        {
            var isEmpty = true;

            foreach (JsonProperty property in element.EnumerateObject())
            {
                isEmpty = false;
                EnterContext(property.Name);
                VisitValue(property.Value);
                ExitContext();
            }

            SetNullIfElementIsEmpty(isEmpty);
        }

        private void VisitArrayElement(JsonElement element)
        {
            int index = 0;

            foreach (JsonElement arrayElement in element.EnumerateArray())
            {
                EnterContext(index.ToString());
                VisitValue(arrayElement);
                ExitContext();
                index++;
            }

            SetNullIfElementIsEmpty(isEmpty: index == 0);
        }

        private void SetNullIfElementIsEmpty(bool isEmpty)
        {
            if (isEmpty && _paths.Count > 0)
            {
                _data[_paths.Peek()] = null;
            }
        }

        private void VisitValue(JsonElement value)
        {
            Debug.Assert(_paths.Count > 0);

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObjectElement(value);
                    break;

                case JsonValueKind.Array:
                    VisitArrayElement(value);
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    string key = _paths.Peek();
                    _data[key] = value.ToString();
                    break;

                default:
                    break;
            }
        }

        private void EnterContext(string context) =>
            _paths.Push(_paths.Count > 0 ?
                _paths.Peek() + ConfigurationPath.KeyDelimiter + context :
                context);

        private void ExitContext() => _paths.Pop();
    }
}
