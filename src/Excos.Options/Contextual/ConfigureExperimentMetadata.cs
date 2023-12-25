// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Contextual;

internal class ConfigureFeatureMetadata : IConfigureOptions
{
    private readonly FeatureMetadata? _metadata;
    private readonly string _propertyName;

    public ConfigureFeatureMetadata(FeatureMetadata? metadata, string propertyName)
    {
        _metadata = metadata;
        _propertyName = propertyName;
    }

    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        var property = typeof(TOptions).GetProperty(_propertyName);
        property?.SetValue(input, _metadata);
    }
}
