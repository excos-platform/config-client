// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

namespace Excos.Options.Contextual;

internal class ConfigureExperimentMetadata : IConfigureOptions
{
    private readonly ExperimentMetadata? _metadata;

    public ConfigureExperimentMetadata(ExperimentMetadata? metadata)
    {
        _metadata = metadata;
    }

    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        foreach (var property in typeof(TOptions).GetProperties())
        {
            if (property.PropertyType == typeof(ExperimentMetadata))
            {
                property.SetValue(input, _metadata);
                return;
            }
        }
    }
}
