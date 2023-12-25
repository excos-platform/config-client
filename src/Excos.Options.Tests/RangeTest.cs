// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Xunit;

namespace Excos.Options.Tests;

public class RangeTest
{
    [Theory]
    [InlineData("(0; 1)")]
    [InlineData("(0.2; 0.3)")]
    [InlineData("[0.5; 1)")]
    [InlineData(" ( 0.5 ; 2 ] ")]
    public void Range_double_ParsesString(string input)
    {
        Assert.True(Range<double>.TryParse(input, null, out _));
    }

    [Theory]
    [InlineData("(2023-01-01T13:45:00+00;2023-01-02T12:26:00+00)")]
    public void Range_DateTime_ParsesString(string input)
    {
        Assert.True(Range<DateTime>.TryParse(input, null, out _));
    }

    [Theory]
    [InlineData("(0; 1)", 0.5, true)]
    [InlineData("(0; 1)", 0, false)]
    [InlineData("(0; 1)", 1, false)]
    [InlineData("[0; 1)", 0, true)]
    [InlineData("(0; 1]", 1, true)]
    [InlineData("[0; 1]", 2, false)]
    public void Range_double_Contains(string input, double value, bool contains)
    {
        Assert.True(Range<double>.TryParse(input, null, out var range));
        Assert.Equal(contains, range.Contains(value));
    }
}
