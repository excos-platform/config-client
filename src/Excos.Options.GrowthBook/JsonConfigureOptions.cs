﻿// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Text.Json;
using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.GrowthBook;

internal class JsonConfigureOptions : IConfigureOptions
{
    private readonly IConfiguration _value;
    public JsonConfigureOptions(string singleValueKey, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            _value = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] { KeyValuePair.Create(singleValueKey, value.GetString()) })
                .Build();
        }
        else
        {
            _value = new ConfigurationBuilder()
                .AddInMemoryCollection(JsonConfigurationFileParser.Parse(value))
                .Build();
        }
    }
    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        _value.GetSection(section).Bind(input);
    }

    /// <summary>
    /// Copied from Microsoft.Extensions.Configuration.Json
    /// </summary>
    internal sealed class JsonConfigurationFileParser
    {
        private JsonConfigurationFileParser() { }

        private readonly Dictionary<string, string?> _data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _paths = new Stack<string>();

        public static IDictionary<string, string?> Parse(JsonElement input)
        {
            var parser = new JsonConfigurationFileParser();
            parser.VisitObjectElement(input);
            return parser._data;
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
                    if (_data.ContainsKey(key))
                    {
                        throw new FormatException($"Error_KeyIsDuplicated {key}");
                    }
                    _data[key] = value.ToString();
                    break;

                default:
                    throw new FormatException($"Error_KeyIsDuplicated {value.ValueKind}");
            }
        }

        private void EnterContext(string context) =>
            _paths.Push(_paths.Count > 0 ?
                _paths.Peek() + ConfigurationPath.KeyDelimiter + context :
                context);

        private void ExitContext() => _paths.Pop();
    }
}

