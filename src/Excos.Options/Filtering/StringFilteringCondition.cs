// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options;

public class StringFilteringCondition : IFilteringCondition
{
    private readonly string _source;

    public StringFilteringCondition(string source)
    {
        _source = source;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        return string.Equals(value?.ToString(), _source, StringComparison.InvariantCultureIgnoreCase);
    }
}
