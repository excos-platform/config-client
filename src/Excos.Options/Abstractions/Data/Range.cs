// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

public struct Range
{
    public Range(double start, double end, RangeType type)
    {
        if (start > end)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater or equal to Start.");
        }

        Start = start;
        End = end;
        Type = type;
    }

    public double Start { get; }
    public double End { get; }
    public RangeType Type { get; }

    public bool Contains(double value)
    {
        if (Type.HasFlag(RangeType.IncludeStart))
        {
            if (value < Start)
            {
                return false;
            }
        }
        else
        {
            if (value <= Start)
            {
                return false;
            }
        }

        if (Type.HasFlag(RangeType.IncludeEnd))
        {
            if (value > End)
            {
                return false;
            }
        }
        else
        {
            if (value >= End)
            {
                return false;
            }
        }

        return true;
    }
}

[Flags]
public enum RangeType
{
    ExcludeBoth = 0,
    IncludeStart = 1,
    IncludeEnd = 2,
    IncludeBoth = 3,
}
