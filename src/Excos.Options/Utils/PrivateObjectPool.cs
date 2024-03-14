// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;

namespace Excos.Options.Utils;

/// <summary>
/// A simple object pool for reusing objects (static properties).
/// </summary>
public static class PrivateObjectPool
{
    /// <summary>
    /// Whether to enable pooling (default: <c>true</c>).
    /// </summary>
    public static bool EnablePooling { get; set; } = true;
}

/// <summary>
/// Basic object pool which returns <c>null</c> if there's no items left in the pool.
/// The caller is responsible for instantiating new instances.
/// </summary>
public sealed class PrivateObjectPool<T> where T : class
{
    /// <summary>
    /// Singleton instance of the <see cref="PrivateObjectPool{T}"/>.
    /// </summary>
    public static readonly PrivateObjectPool<T> Instance = new();

    private readonly ThreadLocal<Stack<T>> _pool = new(() => new Stack<T>(8), trackAllValues: false);

    /// <summary>
    /// Tries to get an instance from the pool.
    /// </summary>
    /// <param name="instance">Pooled object instance.</param>
    /// <returns>True if an instance was retrieved, false otherwise.</returns>
    public bool TryGet([MaybeNullWhen(false)] out T? instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            return _pool.Value!.TryPop(out instance);
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Returns (or adds) an instance to the pool.
    /// </summary>
    /// <param name="instance">Pooled object instance.</param>
    public void Return(T instance)
    {
        if (PrivateObjectPool.EnablePooling)
        {
            _pool.Value!.Push(instance);
        }
    }
}

/// <summary>
/// Attribute used for source generation to mark classes that should be pooled.
/// Note that this is used internally in this repo and may not be applicable to other projects.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PrivatePoolAttribute : Attribute
{
}
