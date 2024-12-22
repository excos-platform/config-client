// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options.Contextual;

/// <summary>
/// For configure options types which can be pooled, upon contextual configuration finish is notified that it can be returned to the pool.
/// </summary>
public interface IPooledConfigureOptions : IConfigureOptions
{
    /// <summary>
    /// Returns the instance to the pool.
    /// </summary>
    public void ReturnToPool();
}
