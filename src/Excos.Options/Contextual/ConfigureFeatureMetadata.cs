// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Utils;

namespace Excos.Options.Contextual;

[PrivatePool]
internal partial class ConfigureFeatureMetadata : IPooledConfigureOptions
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
}
