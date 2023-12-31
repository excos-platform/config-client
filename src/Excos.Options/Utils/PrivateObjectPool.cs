// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;

namespace Excos.Options.Utils;

public static class PrivateObjectPool
{
    public static bool EnablePooling { get; set; } = true;
}

/// <summary>
/// Basic object pool which returns <c>null</c> if there's no items left in the pool.
/// The caller is responsible for instantiating new instances.
/// </summary>
public sealed class PrivateObjectPool<T> where T : class
{
    public static readonly PrivateObjectPool<T> Instance = new();

    private readonly ConcurrentBag<T> _pool = new();

    public bool TryGet(out T? instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            return _pool.TryTake(out instance);
        }

        instance = null;
        return false;
    }

    public void Return(T instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            _pool.Add(instance);
        }
    }
}
