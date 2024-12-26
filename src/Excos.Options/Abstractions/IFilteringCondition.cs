// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Abstractions;

/// <summary>
/// A filtering condition function. 
/// </summary>
public interface IFilteringCondition
{
    /// <summary>
    /// Checks whether the provided context satisfies a filtering condition.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <returns>True if condition is satisfied, false otherwise.</returns>
    bool IsSatisfiedBy<TContext>(TContext value)
        where TContext : IOptionsContext;
}
