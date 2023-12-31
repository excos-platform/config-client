// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

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

    private readonly ThreadLocal<Stack<T>> _pool = new(() => new Stack<T>(8), trackAllValues: false);

    public bool TryGet(out T? instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            return _pool.Value!.TryPop(out instance);
        }

        instance = null;
        return false;
    }

    public void Return(T instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            _pool.Value!.Push(instance);
        }
    }
}
