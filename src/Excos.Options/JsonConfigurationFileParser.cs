// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

// Copied from Microsoft.Extensions.Configuration.Json (MIT License)
// Added some modifications.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Excos.Options;

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
