// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Filtering;

/// <summary>
/// A filtering condition that checks if the value is equal to the source string.
/// </summary>
/// <remarks>
/// Matching is case-insensitive and culture-invariant.
/// </remarks>
internal class StringFilteringCondition : PropertyFilteringCondition
{
    private readonly string _source;

    /// <summary>
    /// Creates a new instance of the filter using the <paramref name="source"/> string.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    /// <param name="source">String to match.</param>
    public StringFilteringCondition(string propertyName, string source) : base(propertyName)
    {
        _source = source;
    }

    /// <inheritdoc/>
    protected override bool PropertyPredicate<T>(T value)
    {
        return string.Equals(value?.ToString(), _source, StringComparison.InvariantCultureIgnoreCase);
    }
}
