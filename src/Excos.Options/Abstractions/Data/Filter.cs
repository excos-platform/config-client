// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// A feature filter applied over a user provided context.
/// </summary>
public class Filter
{
    /// <summary>
    /// Name of the property this filter applies to.
    /// </summary>
    public required string PropertyName { get; set; }

    /// <summary>
    /// List of conditions representing this filter.
    /// The filter is satisfied if any of them is matched.
    /// </summary>
    public List<IFilteringCondition> Conditions { get; } = new();

    /// <summary>
    /// Checks if the values satisfies the filter based on the conditions.
    /// At least one condition must be satisfied.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <returns>True if filter is satisfied, false otherwise.</returns>
    public bool IsSatisfiedBy<T>(T value)
    {
        if (Conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in Conditions)
        {
            if (condition.IsSatisfiedBy(value))
            {
                return true;
            }
        }

        return false;
    }
}
