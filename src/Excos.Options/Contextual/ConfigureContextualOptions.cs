// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Utils;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

[PrivatePool]
internal partial class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private string _configurationSection;

    private ConfigureContextualOptions(string configurationSection)
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
        foreach (var configureOptions in ConfigureOptions)
        {
            if (configureOptions is IPooledConfigureOptions pooled)
            {
                pooled.ReturnToPool();
            }
        }

        Clear();
        Return(this);
    }

    private void Clear() => ConfigureOptions.Clear();
}
