// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options.Filtering;

/// <summary>
/// A constant filtering condition that is never satisfied.
/// </summary>
public class NeverFilteringCondition : IFilteringCondition
{
    internal NeverFilteringCondition() { }

    /// <summary>
    /// Singleton instance of the <see cref="NeverFilteringCondition"/>.
    /// </summary>
    public static NeverFilteringCondition Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsSatisfiedBy<T>(T value) => false;
}
