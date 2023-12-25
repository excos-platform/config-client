// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;
using Excos.Options.Abstractions;

namespace Excos.Options.Filtering;

public class RegexFilteringCondition : IFilteringCondition
{
    private readonly Regex _regex;
    public RegexFilteringCondition(string expression)
    {
        _regex = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        return _regex.IsMatch(value?.ToString() ?? string.Empty);
    }
}
