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
    /// Later variants override earlier ones for overlapping keys.
    /// <para>
    /// Uses <see cref="Utf8JsonWriter"/> with a pooled buffer to minimise allocations.
    /// The caller can then deserialize with
    /// <c>JsonSerializer.Deserialize&lt;T&gt;(element, options)</c>.
    /// </para>
    /// </summary>
    public static JsonElement MergeToJsonElement(this IReadOnlyList<Variant> variants)
    {
        if (variants.Count == 0)
        {
            return default;
        }

        if (variants.Count == 1)
        {
            // No merge needed – return as-is (it's already a cloned element).
            return variants[0].Configuration;
        }

        var buffer = RentBufferWriter();
        using var writer = new Utf8JsonWriter(buffer);

        WriteMergedObject(writer, variants);
        writer.Flush();

        var reader = new Utf8JsonReader(buffer.WrittenSpan);
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Writes all variant configurations as a merged JSON object.
    /// </summary>
    private static void WriteMergedObject(Utf8JsonWriter writer, IReadOnlyList<Variant> variants)
    {
        // Fast path: single variant
        int objectCount = 0;
        JsonElement? singleElement = null;
        
        for (int i = 0; i < variants.Count; i++)
        {
            var element = variants[i].Configuration;
            if (element.ValueKind == JsonValueKind.Object)
            {
                objectCount++;
                singleElement = element;
            }
        }

        if (objectCount == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        if (objectCount == 1)
        {
            singleElement!.Value.WriteTo(writer);
            return;
        }

        // Multiple objects - need to merge
        // Use ArrayPool for the elements array
        var pool = ArrayPool<JsonElement>.Shared;
        var elements = pool.Rent(objectCount);
        int idx = 0;
        for (int i = 0; i < variants.Count; i++)
        {
            var element = variants[i].Configuration;
            if (element.ValueKind == JsonValueKind.Object)
            {
                elements[idx++] = element;
            }
        }

        WriteMergedObjectFromList(writer, elements.AsSpan(0, idx));
        pool.Return(elements);
    }

    /// <summary>
    /// Writes a merged object from a span of JsonElements (2+ items).
    /// Uses ArrayPool for nested recursion to minimize allocations.
    /// </summary>
    private static void WriteMergedObjectFromList(Utf8JsonWriter writer, ReadOnlySpan<JsonElement> elements)
    {
        // Collect all properties with last-wins, tracking which need deep merge
        var merged = new Dictionary<string, (JsonElement Value, bool NeedsMerge)>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            foreach (var property in element.EnumerateObject())
            {
                bool needsMerge = false;
                if (merged.TryGetValue(property.Name, out var existing))
                {
                    needsMerge = existing.Value.ValueKind == JsonValueKind.Object && 
                                 property.Value.ValueKind == JsonValueKind.Object;
                }
                merged[property.Name] = (property.Value, needsMerge);
            }
        }

        writer.WriteStartObject();

        foreach (var (key, (value, needsMerge)) in merged)
        {
            writer.WritePropertyName(key);
            
            if (needsMerge)
            {
                // Use ArrayPool for the contributions array
                var pool = ArrayPool<JsonElement>.Shared;
                var contributions = pool.Rent(elements.Length);
                int count = 0;
                
                foreach (var element in elements)
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase) &&
                            property.Value.ValueKind == JsonValueKind.Object)
                        {
                            contributions[count++] = property.Value;
                        }
                    }
                }
                
                WriteMergedObjectFromList(writer, contributions.AsSpan(0, count));
                pool.Return(contributions);
            }
            else
            {
                value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}
