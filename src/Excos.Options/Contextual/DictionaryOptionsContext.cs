// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

/// <summary>
/// An <see cref="IOptionsContext"/> implementation backed by a string dictionary.
/// Useful for scenarios where context is provided as key-value pairs rather than a typed object.
/// </summary>
public class DictionaryOptionsContext : IOptionsContext
{
    private readonly IDictionary<string, string> _values;

    /// <summary>
    /// Creates a new instance with the specified key-value pairs.
    /// </summary>
    /// <param name="values">Dictionary of context values.</param>
    public DictionaryOptionsContext(IDictionary<string, string> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    /// <inheritdoc/>
    public void PopulateReceiver<T>(T receiver) where T : IOptionsContextReceiver
    {
        foreach (var (key, value) in _values)
        {
            receiver.Receive(key, value);
        }
    }
}
