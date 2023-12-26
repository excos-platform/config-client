// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions;

/// <summary>
/// A generic configuring function which attempts to apply settings over an options object.
/// </summary>
public interface IConfigureOptions
{
    /// <summary>
    /// Configure <paramref name="input"/> object using <paramref name="section"/> to choose specific data for the options.
    /// </summary>
    /// <typeparam name="TOptions">Options type.</typeparam>
    /// <param name="input">Options object.</param>
    /// <param name="section">Configuration section for the options.</param>
    void Configure<TOptions>(TOptions input, string section) where TOptions : class;
}
