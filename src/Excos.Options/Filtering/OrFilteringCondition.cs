// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Filtering;

internal class OrFilteringCondition : IFilteringCondition
{
    private readonly IFilteringCondition[] _conditions;
    public OrFilteringCondition(params IFilteringCondition[] conditions)
    {
        _conditions = conditions;
    }
    public bool IsSatisfiedBy<T>(T value) where T : IOptionsContext
    {
        foreach (var condition in _conditions)
        {
            if (condition.IsSatisfiedBy(value))
            {
                return true;
            }
        }
        return false;
    }
}
