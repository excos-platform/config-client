// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook;

/// <summary>
/// An IOptionsMonitor implementation that returns a fixed value.
/// Used for standalone scenarios without DI change tracking.
/// </summary>
internal class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _value;

    /// <summary>
    /// Creates a new monitor with a fixed value.
    /// </summary>
    /// <param name="value">The value to return.</param>
    public StaticOptionsMonitor(T value)
    {
        _value = value;
    }

    /// <inheritdoc/>
    public T CurrentValue => _value;

    /// <inheritdoc/>
    public T Get(string? name) => _value;

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
