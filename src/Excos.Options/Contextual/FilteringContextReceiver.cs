// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

/// <summary>
/// Context receiver processes an options context
/// and allows us to later execute the filtering over any options context in somewhat efficient manner (no reflection needed).
/// <br/>
/// For each property we create a filtering closure which accepts a <see cref="Filter"/> argument and executes its <see cref="Filter.IsSatisfiedBy{T}(T)"/> method.
/// The reason why we create closures is to wrap the generic call and allow the caller to the context receiver later to not care about the types of underlying properties.
/// </summary>
internal class FilteringContextReceiver : IOptionsContextReceiver
{
    private Dictionary<string, Func<Filter, bool>> FilteringClosures { get; } = new(StringComparer.InvariantCultureIgnoreCase);

    public void Receive<T>(string key, T value)
    {
        FilteringClosures[key] = (filter) => filter.IsSatisfiedBy(value);
    }

    public bool Satisfies(FilterCollection filters)
    {
        // every filter must be satisfied (F1 AND F2 AND ...)
        foreach (var filter in filters)
        {
            if (!FilteringClosures.TryGetValue(filter.PropertyName, out var closure) || !closure(filter))
            {
                return false;
            }
        }

        return true;
    }
}
