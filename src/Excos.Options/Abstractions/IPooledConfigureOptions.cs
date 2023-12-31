// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions;

/// <summary>
/// For configure options types which can be pooled, upon contextual configuration finish is notified that it can be returned to the pool.
/// </summary>
public interface IPooledConfigureOptions : IConfigureOptions
{
    public void ReturnToPool();
}
