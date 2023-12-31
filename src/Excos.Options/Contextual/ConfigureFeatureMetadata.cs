// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Utils;

namespace Excos.Options.Contextual;

internal class ConfigureFeatureMetadata : IPooledConfigureOptions
{
    private FeatureMetadata? _metadata;
    private string _propertyName;

    private ConfigureFeatureMetadata(FeatureMetadata? metadata, string propertyName)
    {
        _metadata = metadata;
        _propertyName = propertyName;
    }

    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        var property = typeof(TOptions).GetProperty(_propertyName);
        property?.SetValue(input, _metadata);
    }

    public void ReturnToPool() => Return(this);

    public static ConfigureFeatureMetadata Get(FeatureMetadata? metadata, string propertyName)
    {
        if (PrivateObjectPool<ConfigureFeatureMetadata>.Instance.TryGet(out var instance) && instance != null)
        {
            instance._metadata = metadata;
            instance._propertyName = propertyName;
        }
        else
        {
            instance = new ConfigureFeatureMetadata(metadata, propertyName);
        }

        return instance;
    }

    public static void Return(ConfigureFeatureMetadata instance) =>
        PrivateObjectPool<ConfigureFeatureMetadata>.Instance.Return(instance);
}
