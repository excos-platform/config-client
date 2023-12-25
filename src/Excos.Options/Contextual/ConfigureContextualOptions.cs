// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

internal class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string _configurationSection;

    public ConfigureContextualOptions(string configurationSection)
    {
        _configurationSection = configurationSection;
    }

    public List<IConfigureOptions> ConfigureOptions { get; } = new();

    public void Configure(TOptions options)
    {
        foreach (var configureOptions in ConfigureOptions)
        {
            configureOptions.Configure(options, _configurationSection);
        }
    }

    public void Dispose()
    {
        // nothing to do
    }
}
