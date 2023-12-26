// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options.Tests;

public class NullConfigureOptions : IConfigureOptions
{
    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
    }
}
