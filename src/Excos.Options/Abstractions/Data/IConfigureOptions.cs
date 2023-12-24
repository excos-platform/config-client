// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// A generic configuring function which attempts to apply settings over an options object.
/// </summary>
public interface IConfigureOptions
{
    void Configure<TOptions>(TOptions input, string section) where TOptions : class;
}
