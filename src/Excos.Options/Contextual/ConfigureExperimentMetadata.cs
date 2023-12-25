// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Contextual;

internal class ConfigureExperimentMetadata : IConfigureOptions
{
    private readonly ExperimentMetadata? _metadata;
    private readonly string _propertyName;

    public ConfigureExperimentMetadata(ExperimentMetadata? metadata, string propertyName)
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
