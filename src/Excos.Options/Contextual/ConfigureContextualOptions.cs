// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

internal partial class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string _configurationSection;
    private readonly IReadOnlyList<Variant> _variants;

    public ConfigureContextualOptions(string configurationSection, IEnumerable<Variant> variants)
    {
        _configurationSection = configurationSection;
        _variants = variants.ToList();
    }

    public void Configure(TOptions options)
    {
        var configureAction = VariantConfigurationUtilities.ToConfigureAction<TOptions>(_variants, _configurationSection);
        configureAction(options);
    }

    public void Dispose()
    {
    }
}
