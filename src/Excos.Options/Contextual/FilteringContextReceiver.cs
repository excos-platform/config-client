// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

/// <summary>
/// Context receiver processes an options context
/// and allows us to later execute the filtering over any options context in somewhat efficient manner (no reflection needed).
/// <br/>
/// For each property we create a filtering closure which accepts a <see cref="Filter"/> argument and executes its <see cref="Filter.IsSatisfiedBy{T}(T)"/> method.
/// The reason why we create closures is to wrap the generic call and allow the caller to the context receiver later to not care about the types of underlying properties.
/// </summary>
internal class FilteringContextReceiver : IOptionsContextReceiver, IDisposable
{
    private Dictionary<string, FilteringClosure> FilteringClosures { get; } = new(StringComparer.InvariantCultureIgnoreCase);

    private FilteringContextReceiver() { }

    public void Receive<T>(string key, T value)
    {
        FilteringClosures[key] = FilteringClosure<T>.Get(value);
    }

    public bool Satisfies(FilterCollection filters)
    {
        // every filter must be satisfied (F1 AND F2 AND ...)
        foreach (var filter in filters)
        {
            if (!FilteringClosures.TryGetValue(filter.PropertyName, out var closure) || !closure.Invoke(filter))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var closure in FilteringClosures.Values)
        {
            closure.Dispose();
        }

        Return(this);
    }

    public static FilteringContextReceiver Get()
    {
        if (PrivateObjectPool<FilteringContextReceiver>.Instance.TryGet(out var instance) && instance != null)
        {
            instance.FilteringClosures.Clear();
        }
        else
        {
            instance = new FilteringContextReceiver();
        }

        return instance;
    }

    public static void Return(FilteringContextReceiver instance) =>
        PrivateObjectPool<FilteringContextReceiver>.Instance.Return(instance);

    private abstract class FilteringClosure : IDisposable
    {
        public abstract bool Invoke(Filter filter);
        public abstract void Dispose();
    }

    private sealed class FilteringClosure<T> : FilteringClosure
    {
        private T _value;

        private FilteringClosure(T value) => _value = value;
        
        public override void Dispose() => Return(this);
        public override bool Invoke(Filter filter) => filter.IsSatisfiedBy(_value);

        public static FilteringClosure<T> Get(T value)
        {
            if (PrivateObjectPool<FilteringClosure<T>>.Instance.TryGet(out var instance) && instance != null)
            {
                instance._value = value;
            }
            else
            {
                instance = new FilteringClosure<T>(value);
            }

            return instance;
        }

        public static void Return(FilteringClosure<T> instance) =>
            PrivateObjectPool<FilteringClosure<T>>.Instance.Return(instance);
    }
}
