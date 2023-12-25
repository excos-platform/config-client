// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions;

/// <summary>
/// A filtering condition function. 
/// </summary>
public interface IFilteringCondition
{
    /// <summary>
    /// Checks whether the provided value satisfies a filtering condition.
    /// </summary>
    /// <remarks>
    /// Implementer may choose what types <typeparamref name="T"/> it supports and return false for all other ones.
    /// </remarks>
    /// <param name="value">Value.</param>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <returns>True if condition is satisfied, false otherwise.</returns>
    bool IsSatisfiedBy<T>(T value);
}
