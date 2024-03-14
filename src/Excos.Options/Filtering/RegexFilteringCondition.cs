// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;
using Excos.Options.Abstractions;

namespace Excos.Options.Filtering;

/// <summary>
/// A filtering condition that uses a regular expression to match the value.
/// </summary>
/// <remarks>
/// Matching is case-insensitive and culture-invariant.
/// </remarks>
public class RegexFilteringCondition : IFilteringCondition
{
    private readonly Regex _regex;

    /// <summary>
    /// Creates a new instance of the filter using the Regex <paramref name="expression"/>.
    /// </summary>
    /// <param name="expression">Regular expression acceptable by <see cref="Regex"/>.</param>
    public RegexFilteringCondition(string expression)
    {
        _regex = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <inheritdoc/>
    public bool IsSatisfiedBy<T>(T value)
    {
        return _regex.IsMatch(value?.ToString() ?? string.Empty);
    }
}
