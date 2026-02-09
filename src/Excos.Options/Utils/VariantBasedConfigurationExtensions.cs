// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Utils;

internal static class VariantBasedConfigurationExtensions
{
    /// <summary>
    /// Computes a stable hash for a collection of variants based on their IDs.
    /// </summary>
    /// <param name="variants">The variants to hash.</param>
    /// <returns>A 64-bit hash value.</returns>
    public static ulong ComputeVariantHash(this IEnumerable<Variant> variants)
    {
        var hasher = new XxHash64();
        foreach (var variant in variants)
        {
            hasher.Append(MemoryMarshal.AsBytes(variant.Id.AsSpan()));
        }
        return hasher.GetCurrentHashAsUInt64();
    }
    /// <summary>
    /// Converts multiple variant configurations to a merged configuration dictionary.
    /// Later variants override earlier ones for the same keys.
    /// </summary>
    /// <param name="variants">The variants whose configurations should be merged.</param>
    /// <returns>A merged dictionary with colon-delimited keys.</returns>
    public static Dictionary<string, string?> ToConfigurationDictionary(this IEnumerable<Variant> variants)
    {
        var mergedConfig = variants.MergeToJsonElement();
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var pathBuilder = new System.Text.StringBuilder(128);
        VisitElement(mergedConfig, pathBuilder, result);
        return result;
    }

    /// <summary>
    /// Merges multiple variant configurations into a single <see cref="IConfiguration"/> instance.
    /// Later variants override earlier ones for the same keys.
    /// </summary>
    /// <param name="variants">The variants whose configurations should be merged.</param>
    /// <returns>An <see cref="IConfiguration"/> containing the merged configuration data.</returns>
    public static IConfiguration ToConfiguration(this IEnumerable<Variant> variants)
    {
        var data = ToConfigurationDictionary(variants);
        var provider = new MemoryConfigurationProvider(data);
        return new ConfigurationRoot(new IConfigurationProvider[] { provider });
    }

    private static void VisitElement(JsonElement element, System.Text.StringBuilder pathBuilder, Dictionary<string, string?> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var initialLength = pathBuilder.Length;
                foreach (var property in element.EnumerateObject())
                {
                    if (pathBuilder.Length > 0)
                    {
                        pathBuilder.Append(':');
                    }
                    pathBuilder.Append(property.Name);
                    VisitElement(property.Value, pathBuilder, result);
                    pathBuilder.Length = initialLength; // Reset to previous length
                }
                break;

            case JsonValueKind.Array:
                initialLength = pathBuilder.Length;
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (pathBuilder.Length > 0)
                    {
                        pathBuilder.Append(':');
                    }
                    pathBuilder.Append(index);
                    VisitElement(item, pathBuilder, result);
                    pathBuilder.Length = initialLength; // Reset to previous length
                    index++;
                }
                break;

            case JsonValueKind.String:
                result[pathBuilder.ToString()] = element.GetString();
                break;

            case JsonValueKind.Number:
                result[pathBuilder.ToString()] = element.GetRawText();
                break;

            case JsonValueKind.True:
                result[pathBuilder.ToString()] = "True";
                break;

            case JsonValueKind.False:
                result[pathBuilder.ToString()] = "False";
                break;

            case JsonValueKind.Null:
                result[pathBuilder.ToString()] = null;
                break;

            case JsonValueKind.Undefined:
                // Skip undefined values
                break;
        }
    }

    private class MemoryConfigurationProvider : ConfigurationProvider
    {
        public MemoryConfigurationProvider(Dictionary<string, string?> data)
        {
            Data = data;
        }
    }
}
