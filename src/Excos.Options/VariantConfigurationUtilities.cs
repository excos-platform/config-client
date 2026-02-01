// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

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
    /// Converts an enumerable of variants into a configuration dictionary.
    /// </summary>
    /// <param name="variants">The variants to convert.</param>
    /// <param name="sectionPrefix">Optional prefix for configuration keys.</param>
    /// <returns>A dictionary suitable for use with the configuration framework.</returns>
    public static IDictionary<string, string?> ToConfigurationDictionary(
        IEnumerable<Variant> variants,
        string? sectionPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(variants);

        var inputs = variants.Select(v =>
        {
            // Each variant should have a feature name as context
            // We'll use empty string as default if no prefix
            var prefix = sectionPrefix ?? string.Empty;
            return (prefix, v.Configuration);
        });

        return JsonConfigurationFileParser.Parse(inputs);
    }

    /// <summary>
    /// Converts an enumerable of variants into an IConfiguration.
    /// </summary>
    /// <param name="variants">The variants to convert.</param>
    /// <param name="sectionPrefix">Optional prefix for configuration keys.</param>
    /// <returns>An IConfiguration built from the variants.</returns>
    public static IConfiguration ToConfiguration(
        IEnumerable<Variant> variants,
        string? sectionPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(variants);

        var dictionary = ToConfigurationDictionary(variants, sectionPrefix);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary)
            .Build();
    }

    /// <summary>
    /// Creates a configure action that binds variant configurations to an options object.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <param name="variants">The variants to convert.</param>
    /// <param name="section">The configuration section name. Use empty string to bind the entire configuration.</param>
    /// <returns>An action that configures the options object.</returns>
    public static Action<TOptions> ToConfigureAction<TOptions>(
        IEnumerable<Variant> variants,
        string section)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(section);

        // Materialize the variants to avoid multiple enumeration
        var variantList = variants.ToList();
        
        return options =>
        {
            var configuration = ToConfiguration(variantList);
            
            // If section is empty, bind the entire configuration, otherwise bind the specified section
            if (string.IsNullOrEmpty(section))
            {
                configuration.Bind(options);
            }
            else
            {
                configuration.GetSection(section).Bind(options);
            }
        };
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
}
