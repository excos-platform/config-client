// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Microsoft.Extensions.Configuration;

namespace Excos.Options.Providers.Configuration.FilterParsers;

internal class RangeFilterParser : IFeatureFilterParser
{
    public bool TryParseFilter(string propertyName, IConfiguration configuration, [NotNullWhen(true)] out IFilteringCondition? filteringCondition)
    {
        var pattern = configuration.Get<string?>();
        if (pattern == null)
        {
            filteringCondition = null;
            return false;
        }

        if (Range<Guid>.TryParse(pattern, null, out var guidRange))
        {
            filteringCondition = new RangeFilteringCondition<Guid>(propertyName, guidRange);
            return true;
        }
        if (Range<DateTimeOffset>.TryParse(pattern, null, out var dateRange))
        {
            filteringCondition = new RangeFilteringCondition<DateTimeOffset>(propertyName, dateRange);
            return true;
        }
        if (Range<double>.TryParse(pattern, null, out var doubleRange))
        {
            filteringCondition = new RangeFilteringCondition<double>(propertyName, doubleRange);
            return true;
        }

        filteringCondition = null;
        return false;
    }
}
