// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

internal partial class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string _configurationSection;

    public ConfigureContextualOptions(string configurationSection)
    {
        _configurationSection = configurationSection;
    }

    public List<Variant> Variants { get; } = new(8);

    public void Configure(TOptions options)
    {
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TOptions>(Variants, _configurationSection);
        configureAction(options);
    }

    public void Dispose()
    {
    }
}
