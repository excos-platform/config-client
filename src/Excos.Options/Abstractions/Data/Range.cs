// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// A range represents a spectrum of comparable values between a start and an end.
/// </summary>
/// <typeparam name="T">Type of values in the range.</typeparam>
public readonly struct Range<T> : ISpanParsable<Range<T>> where T : IComparable<T>, ISpanParsable<T>
{
    /// <summary>
    /// Creates a new range from <paramref name="start"/> to <paramref name="end"/>.
    /// </summary>
    /// <remarks>
    /// Ensures that <paramref name="end"/> must be greater or equal to <paramref name="start"/>.
    /// </remarks> 
    /// <param name="start">Start value.</param>
    /// <param name="end">End value.</param>
    /// <param name="type">Whether the ends of the range are included or excluded.</param>
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

    /// <summary>
    /// Beginning of the range.
    /// </summary>
    public T Start { get; }

    /// <summary>
    /// End of the range.
    /// </summary>
    public T End { get; }

    /// <summary>
    /// Type of the range in terms of inclusivity of beginning and end.
    /// </summary>
    public RangeType Type { get; }

    /// <summary>
    /// Checks if the value is contained within the range.
    /// </summary>
    public readonly bool Contains(T value)
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Range<T> result)
        => TryParse(s.AsSpan(), provider, out result);

    /// <inheritdoc/>
    public static Range<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new ArgumentException("Couldn't parse the range.");

    /// <inheritdoc/>
    public static Range<T> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);
}

/// <summary>
/// Type of the range in terms of inclusivity of beginning and end.
/// </summary>
[Flags]
public enum RangeType
{
    /// <summary>
    /// Range is open on both ends.
    /// </summary>
    ExcludeBoth = 0,

    /// <summary>
    /// Range is closed on the left, the start value is part of the range.
    /// </summary>
    IncludeStart = 1,

    /// <summary>
    /// Range is closed on the right, the end value is part of the range.
    /// </summary>
    IncludeEnd = 2,

    /// <summary>
    /// Range is closed on both ends.
    /// </summary>
    IncludeBoth = 3,
}
