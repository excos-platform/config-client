// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;

namespace Excos.Options.Abstractions.Data;

public struct Range<T> : ISpanParsable<Range<T>> where T : IComparable<T>, ISpanParsable<T>
{
    public Range(T start, T end, RangeType type)
    {
        if (start.CompareTo(end) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater or equal to Start.");
        }

        Start = start;
        End = end;
        Type = type;
    }

    public T Start { get; }
    public T End { get; }
    public RangeType Type { get; }

    public bool Contains(T value)
    {
        if (Type.HasFlag(RangeType.IncludeStart))
        {
            if (value.CompareTo(Start) < 0)
            {
                return false;
            }
        }
        else
        {
            if (value.CompareTo(Start) <= 0)
            {
                return false;
            }
        }

        if (Type.HasFlag(RangeType.IncludeEnd))
        {
            if (value.CompareTo(End) > 0)
            {
                return false;
            }
        }
        else
        {
            if (value.CompareTo(End) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out Range<T> result)
    {
        var rangeType = RangeType.ExcludeBoth;
        result = default;
        s = s.Trim();
        if (s.Length < 3)
        {
            return false;
        }

        if (s[0] == '[')
        {
            rangeType |= RangeType.IncludeStart;
        }
        else if (s[0] != '(')
        {
            // expected the first character to be [ or (
            return false;
        }

        if (s[^1] == ']')
        {
            rangeType |= RangeType.IncludeEnd;
        }
        else if (s[^1] != ')')
        {
            // expected the last character to be ] or )
            return false;
        }

        var indexOfSeparator = s.IndexOf(';');
        var startSpan = s[1..indexOfSeparator].Trim();
        var endSpan = s[(indexOfSeparator + 1)..^1].Trim();

        if (T.TryParse(startSpan, provider, out var start) && T.TryParse(endSpan, provider, out var end) &&
            start.CompareTo(end) <= 0)
        {
            result = new Range<T>(start, end, rangeType);
            return true;
        }

        return false;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Range<T> result)
        => TryParse(s.AsSpan(), provider, out result);

    public static Range<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new ArgumentException("Couldn't parse the range.");

    public static Range<T> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);
}

[Flags]
public enum RangeType
{
    ExcludeBoth = 0,
    IncludeStart = 1,
    IncludeEnd = 2,
    IncludeBoth = 3,
}
