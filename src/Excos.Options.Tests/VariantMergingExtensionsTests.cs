// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
using Xunit;

namespace Excos.Options.Tests;

public class VariantMergingExtensionsTests
{
    #region Basic Scenarios

    [Fact]
    public void MergeToJsonElement_EmptyEnumerable_ReturnsDefault()
    {
        var variants = Enumerable.Empty<Variant>();
        var result = variants.MergeToJsonElement();
        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }

    [Fact]
    public void MergeToJsonElement_SingleVariant_ReturnsSameElement()
    {
        var json = JsonDocument.Parse("""{"Key":"Value"}""").RootElement;
        var variants = new[] { MakeVariant(json) };

        var result = variants.MergeToJsonElement();

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("Value", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void MergeToJsonElement_NonObjectVariantsOnly_ReturnsDefault()
    {
        var json = JsonDocument.Parse("""42""").RootElement;
        var variants = new[] { MakeVariant(json) };

        var result = variants.MergeToJsonElement();
        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }

    #endregion

    #region Object Merging — Non-Overlapping Keys

    [Fact]
    public void MergeToJsonElement_DisjointKeys_AllKeysPresent()
    {
        var v1 = MakeVariant("""{"A":"1"}""");
        var v2 = MakeVariant("""{"B":"2"}""");
        var v3 = MakeVariant("""{"C":"3"}""");

        var result = new[] { v1, v2, v3 }.MergeToJsonElement();

        Assert.Equal("1", result.GetProperty("A").GetString());
        Assert.Equal("2", result.GetProperty("B").GetString());
        Assert.Equal("3", result.GetProperty("C").GetString());
    }

    #endregion

    #region Primitives — Last Wins

    [Fact]
    public void MergeToJsonElement_OverlappingPrimitiveKey_LastWins()
    {
        var v1 = MakeVariant("""{"Key":"First"}""");
        var v2 = MakeVariant("""{"Key":"Second"}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.Equal("Second", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void MergeToJsonElement_ThreeVariantsSamePrimitiveKey_LastWins()
    {
        var v1 = MakeVariant("""{"Key":"A"}""");
        var v2 = MakeVariant("""{"Key":"B"}""");
        var v3 = MakeVariant("""{"Key":"C"}""");

        var result = new[] { v1, v2, v3 }.MergeToJsonElement();

        Assert.Equal("C", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void MergeToJsonElement_OverlappingNumericKey_LastWins()
    {
        var v1 = MakeVariant("""{"Count":10}""");
        var v2 = MakeVariant("""{"Count":42}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.Equal(42, result.GetProperty("Count").GetInt32());
    }

    [Fact]
    public void MergeToJsonElement_OverlappingBooleanKey_LastWins()
    {
        var v1 = MakeVariant("""{"Enabled":false}""");
        var v2 = MakeVariant("""{"Enabled":true}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.True(result.GetProperty("Enabled").GetBoolean());
    }

    #endregion

    #region Deep Merge — Nested Objects

    [Fact]
    public void MergeToJsonElement_NestedObjects_DeepMerge()
    {
        var v1 = MakeVariant("""{"Section":{"A":"1"}}""");
        var v2 = MakeVariant("""{"Section":{"B":"2"}}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var section = result.GetProperty("Section");
        Assert.Equal("1", section.GetProperty("A").GetString());
        Assert.Equal("2", section.GetProperty("B").GetString());
    }

    [Fact]
    public void MergeToJsonElement_DeeplyNestedObjects_DeepMerge()
    {
        var v1 = MakeVariant("""{"L1":{"L2":{"A":"1"}}}""");
        var v2 = MakeVariant("""{"L1":{"L2":{"B":"2"}}}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var l2 = result.GetProperty("L1").GetProperty("L2");
        Assert.Equal("1", l2.GetProperty("A").GetString());
        Assert.Equal("2", l2.GetProperty("B").GetString());
    }

    [Fact]
    public void MergeToJsonElement_NestedObjectOverlappingPrimitive_LastWins()
    {
        var v1 = MakeVariant("""{"Section":{"Key":"Old","Other":"Keep"}}""");
        var v2 = MakeVariant("""{"Section":{"Key":"New"}}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var section = result.GetProperty("Section");
        Assert.Equal("New", section.GetProperty("Key").GetString());
        Assert.Equal("Keep", section.GetProperty("Other").GetString());
    }

    #endregion

    #region Array Concatenation

    [Fact]
    public void MergeToJsonElement_ArraysSameKey_Concatenated()
    {
        var v1 = MakeVariant("""{"Tags":["a","b"]}""");
        var v2 = MakeVariant("""{"Tags":["c","d"]}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var tags = result.GetProperty("Tags");
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        var items = tags.EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["a", "b", "c", "d"], items);
    }

    [Fact]
    public void MergeToJsonElement_ThreeArraysSameKey_AllConcatenated()
    {
        var v1 = MakeVariant("""{"Items":[1]}""");
        var v2 = MakeVariant("""{"Items":[2,3]}""");
        var v3 = MakeVariant("""{"Items":[4]}""");

        var result = new[] { v1, v2, v3 }.MergeToJsonElement();

        var items = result.GetProperty("Items").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        Assert.Equal([1, 2, 3, 4], items);
    }

    [Fact]
    public void MergeToJsonElement_EmptyArrayConcatenation_PreservesOther()
    {
        var v1 = MakeVariant("""{"Tags":["a","b"]}""");
        var v2 = MakeVariant("""{"Tags":[]}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var items = result.GetProperty("Tags").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["a", "b"], items);
    }

    [Fact]
    public void MergeToJsonElement_NestedObjectsContainingArrays_ArraysConcatenated()
    {
        var v1 = MakeVariant("""{"Section":{"Tags":["a"],"Key":"1"}}""");
        var v2 = MakeVariant("""{"Section":{"Tags":["b"],"Key":"2"}}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var section = result.GetProperty("Section");
        Assert.Equal("2", section.GetProperty("Key").GetString());
        var tags = section.GetProperty("Tags").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["a", "b"], tags);
    }

    [Fact]
    public void MergeToJsonElement_ArraysOfObjects_Concatenated()
    {
        var v1 = MakeVariant("""{"Items":[{"Id":1}]}""");
        var v2 = MakeVariant("""{"Items":[{"Id":2}]}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        var items = result.GetProperty("Items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(1, items[0].GetProperty("Id").GetInt32());
        Assert.Equal(2, items[1].GetProperty("Id").GetInt32());
    }

    #endregion

    #region Mixed Types — Last Wins

    [Fact]
    public void MergeToJsonElement_ObjectThenPrimitive_LastWins()
    {
        var v1 = MakeVariant("""{"Key":{"Nested":"Value"}}""");
        var v2 = MakeVariant("""{"Key":"Overwritten"}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.Equal("Overwritten", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void MergeToJsonElement_ArrayThenPrimitive_LastWins()
    {
        var v1 = MakeVariant("""{"Key":[1,2,3]}""");
        var v2 = MakeVariant("""{"Key":"Replaced"}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.Equal("Replaced", result.GetProperty("Key").GetString());
    }

    [Fact]
    public void MergeToJsonElement_PrimitiveThenArray_LastWins()
    {
        var v1 = MakeVariant("""{"Key":"Original"}""");
        var v2 = MakeVariant("""{"Key":[1,2,3]}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        Assert.Equal(JsonValueKind.Array, result.GetProperty("Key").ValueKind);
    }

    #endregion

    #region IEnumerable Single Iteration

    [Fact]
    public void MergeToJsonElement_EnumerableIsIteratedOnce()
    {
        var v1 = MakeVariant("""{"A":"1"}""");
        var v2 = MakeVariant("""{"B":"2"}""");

        var tracking = new SingleIterationEnumerable<Variant>([v1, v2]);

        var result = tracking.MergeToJsonElement();

        Assert.Equal(1, tracking.EnumerationCount);
        Assert.Equal("1", result.GetProperty("A").GetString());
        Assert.Equal("2", result.GetProperty("B").GetString());
    }

    #endregion

    #region Case Insensitivity

    [Fact]
    public void MergeToJsonElement_CaseInsensitiveKeys_DeepMerges()
    {
        var v1 = MakeVariant("""{"section":{"A":"1"}}""");
        var v2 = MakeVariant("""{"Section":{"B":"2"}}""");

        var result = new[] { v1, v2 }.MergeToJsonElement();

        // Both should merge into the same section (case-insensitive)
        var section = result.EnumerateObject().First(p =>
            string.Equals(p.Name, "section", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1", section.Value.GetProperty("A").GetString());
        Assert.Equal("2", section.Value.GetProperty("B").GetString());
    }

    #endregion

    #region Helpers

    private static Variant MakeVariant(string json)
    {
        return new Variant
        {
            Id = Guid.NewGuid().ToString(),
            Configuration = JsonDocument.Parse(json).RootElement,
            Filters = [],
        };
    }

    private static Variant MakeVariant(JsonElement element)
    {
        return new Variant
        {
            Id = Guid.NewGuid().ToString(),
            Configuration = element,
            Filters = [],
        };
    }

    private sealed class SingleIterationEnumerable<T> : IEnumerable<T>
    {
        private readonly T[] _items;
        public int EnumerationCount { get; private set; }

        public SingleIterationEnumerable(T[] items) => _items = items;

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return ((IEnumerable<T>)_items).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
