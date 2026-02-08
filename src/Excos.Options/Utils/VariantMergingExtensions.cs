// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Buffers;
using System.Text.Json;
using Excos.Options.Abstractions.Data;

namespace Excos.Options.Utils;

internal static class VariantMergingExtensions
{
    /// <summary>
    /// Thread-local buffer writer for JSON merging.
    /// </summary>
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_bufferWriter;

    private static ArrayBufferWriter<byte> RentBufferWriter()
    {
        var writer = t_bufferWriter;
        if (writer == null)
        {
            writer = new ArrayBufferWriter<byte>(512);
            t_bufferWriter = writer;
        }
        else
        {
            writer.ResetWrittenCount();
        }
        return writer;
    }

    /// <summary>
    /// Deep-merges the <see cref="Variant.Configuration"/> elements from all supplied
    /// variants into a single <see cref="JsonElement"/>.
    /// Later variants override earlier ones for overlapping primitive keys.
    /// Overlapping arrays are concatenated. Overlapping objects are deep-merged.
    /// <para>
    /// The <paramref name="variants"/> enumerable is iterated exactly once.
    /// Uses <see cref="Utf8JsonWriter"/> with a pooled buffer to minimise allocations.
    /// </para>
    /// </summary>
    public static JsonElement MergeToJsonElement(this IEnumerable<Variant> variants)
    {
        // Single iteration: collect only object configurations into a pooled array.
        var pool = ArrayPool<JsonElement>.Shared;
        var elements = pool.Rent(8);
        int count = 0;

        foreach (var variant in variants)
        {
            var config = variant.Configuration;
            if (config.ValueKind == JsonValueKind.Object)
            {
                if (count == elements.Length)
                {
                    var larger = pool.Rent(elements.Length * 2);
                    elements.AsSpan(0, count).CopyTo(larger);
                    pool.Return(elements);
                    elements = larger;
                }
                elements[count++] = config;
            }
        }

        if (count == 0)
        {
            pool.Return(elements);
            return default;
        }

        if (count == 1)
        {
            var single = elements[0];
            pool.Return(elements);
            return single;
        }

        var buffer = RentBufferWriter();
        using var writer = new Utf8JsonWriter(buffer);

        WriteMergedObject(writer, elements.AsSpan(0, count));
        pool.Return(elements);
        writer.Flush();

        var reader = new Utf8JsonReader(buffer.WrittenSpan);
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    private enum MergeKind : byte
    {
        /// <summary>Only one variant contributed this key so far.</summary>
        Single,
        /// <summary>All contributing variants had an object — deep-merge.</summary>
        DeepMerge,
        /// <summary>All contributing variants had an array — concatenate.</summary>
        ArrayConcat,
        /// <summary>Types were mixed or primitive — last value wins.</summary>
        LastWins,
    }

    /// <summary>
    /// Writes a merged JSON object from a span of <see cref="JsonElement"/> objects (2+ items).
    /// <para>
    /// Pass 1 — determines the merge strategy for every property key:
    ///   • All objects  → deep-merge recursively.
    ///   • All arrays   → concatenate.
    ///   • Otherwise    → last value wins.
    /// Pass 2 — writes the merged output.
    /// </para>
    /// </summary>
    private static void WriteMergedObject(Utf8JsonWriter writer, ReadOnlySpan<JsonElement> elements)
    {
        var properties = new Dictionary<string, (JsonElement LastValue, MergeKind Kind)>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (!properties.TryGetValue(prop.Name, out var existing))
                {
                    properties[prop.Name] = (prop.Value, MergeKind.Single);
                }
                else
                {
                    var kind = (existing.Kind, existing.LastValue.ValueKind, prop.Value.ValueKind) switch
                    {
                        (MergeKind.Single, JsonValueKind.Object, JsonValueKind.Object) => MergeKind.DeepMerge,
                        (MergeKind.Single, JsonValueKind.Array, JsonValueKind.Array) => MergeKind.ArrayConcat,
                        (MergeKind.DeepMerge, _, JsonValueKind.Object) => MergeKind.DeepMerge,
                        (MergeKind.ArrayConcat, _, JsonValueKind.Array) => MergeKind.ArrayConcat,
                        _ => MergeKind.LastWins,
                    };
                    properties[prop.Name] = (prop.Value, kind);
                }
            }
        }

        writer.WriteStartObject();

        foreach (var (key, (value, kind)) in properties)
        {
            writer.WritePropertyName(key);

            switch (kind)
            {
                case MergeKind.Single:
                case MergeKind.LastWins:
                    value.WriteTo(writer);
                    break;

                case MergeKind.DeepMerge:
                    WriteMergedProperty(writer, key, elements);
                    break;

                case MergeKind.ArrayConcat:
                    WriteConcatenatedArray(writer, key, elements);
                    break;
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Collects all object contributions for <paramref name="key"/> and deep-merges them.
    /// </summary>
    private static void WriteMergedProperty(Utf8JsonWriter writer, string key, ReadOnlySpan<JsonElement> elements)
    {
        var pool = ArrayPool<JsonElement>.Shared;
        var contributions = pool.Rent(elements.Length);
        int count = 0;

        foreach (var element in elements)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.Object)
                {
                    contributions[count++] = prop.Value;
                }
            }
        }

        WriteMergedObject(writer, contributions.AsSpan(0, count));
        pool.Return(contributions);
    }

    /// <summary>
    /// Writes a single JSON array whose elements are the concatenation of all arrays
    /// found under <paramref name="key"/> across the source elements.
    /// </summary>
    private static void WriteConcatenatedArray(Utf8JsonWriter writer, string key, ReadOnlySpan<JsonElement> elements)
    {
        writer.WriteStartArray();

        foreach (var element in elements)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        item.WriteTo(writer);
                    }
                }
            }
        }

        writer.WriteEndArray();
    }
}
