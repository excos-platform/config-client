// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Allocation is a range between 0 and 100% used to determine if a given feature variant should be active.
/// </summary>
public readonly struct Allocation
{
    private readonly Range<double> _range;

    /// <summary>
    /// Initializes a new allocation value, checking the bounds of the range.
    /// </summary>
    /// <param name="range">Range being wrapped.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside of [0, 1].</exception>
    public Allocation(Range<double> range)
    {
        if (range.Start < 0 || range.Start > 1 || range.End < 0 || range.End > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Allocation must be a range between 0 and 1.");
        }

        _range = range;
    }

    public bool Contains(double value) => _range.Contains(value);

    public static Allocation Percentage(double percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Allocation must be a range between 0% and 100%.");
        }

        return new Allocation(new Range<double>(0, percentage / 100, RangeType.IncludeBoth));
    } 
}
