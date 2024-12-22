// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;

namespace Excos.Options.Filtering;

/// <summary>
/// A filtering condition that uses a regular expression to match the value.
/// </summary>
/// <remarks>
/// Matching is case-insensitive and culture-invariant.
/// </remarks>
internal class RegexFilteringCondition : PropertyFilteringCondition
{
    private readonly Regex _regex;

    /// <summary>
    /// Creates a new instance of the filter using the Regex <paramref name="expression"/>.
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    /// <param name="expression">Regular expression acceptable by <see cref="Regex"/>.</param>
    public RegexFilteringCondition(string propertyName, string expression) : base(propertyName)
    {
        _regex = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <inheritdoc/>
    protected override bool PropertyPredicate<T>(T value)
    {
        return _regex.IsMatch(value?.ToString() ?? string.Empty);
    }
}
