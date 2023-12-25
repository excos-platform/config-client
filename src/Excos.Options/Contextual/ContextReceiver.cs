// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.IO.Hashing;
using System.Runtime.InteropServices;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

// TODO explain in a comment how this will work
internal class ContextReceiver : IOptionsContextReceiver
{
    private string _identifier = string.Empty;
    private readonly Dictionary<string, Func<Filter, bool>> FilteringClosures = new(StringComparer.InvariantCultureIgnoreCase);

    public void Receive<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(_identifier) &&
            (string.Equals(key, "Identifier", StringComparison.InvariantCultureIgnoreCase) ||
            key.EndsWith("Id", StringComparison.InvariantCultureIgnoreCase)))
        {
            // we use the first non-empty ID for allocation purposes
            _identifier = value?.ToString() ?? string.Empty;
        }

        FilteringClosures[key] = (filter) => filter.IsSatisfiedBy(value);
    }

    public bool Satisfies(FilterCollection filters)
    {
        // every filter must be satisfied
        foreach (var filter in filters)
        {
            if (!FilteringClosures.TryGetValue(filter.PropertyName, out var closure) || !closure(filter))
            {
                return false;
            }
        }

        return true;
    }

    public double GetIdentifierAllocationSpot(string salt)
    {
        var source = $"{salt}_{_identifier}";
        var hash = XxHash32.HashToUInt32(MemoryMarshal.AsBytes(source.AsSpan()));
        return (double)hash / uint.MaxValue;
    }
}
