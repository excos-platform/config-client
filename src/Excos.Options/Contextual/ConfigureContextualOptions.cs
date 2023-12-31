// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Utils;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

internal class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
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

        ConfigureOptions.Clear();
        Return(this);
    }

    public static ConfigureContextualOptions<TOptions> Get(string configurationSection)
    {
        if (PrivateObjectPool<ConfigureContextualOptions<TOptions>>.Instance.TryGet(out var instance) && instance != null)
        {
            instance._configurationSection = configurationSection;
        }
        else
        {
            instance = new ConfigureContextualOptions<TOptions>(configurationSection);
        }

        instance.ConfigureOptions.Clear();
        return instance;
    }

    public static void Return(ConfigureContextualOptions<TOptions> instance) =>
        PrivateObjectPool<ConfigureContextualOptions<TOptions>>.Instance.Return(instance);
}
