// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

public struct Allocation
{
    private readonly Range _range;
    public Allocation(Range range)
    {
        if (range.Start < 0 || range.Start > 1 || range.End < 0 || range.End > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Allocation must be a range between 0 and 1.");
        }

        _range = range;
    }

    public bool Contains(double value) => _range.Contains(value);
}
