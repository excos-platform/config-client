// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

internal partial class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string _configurationSection;
    private readonly IEnumerable<Variant> _variants;

    public ConfigureContextualOptions(string configurationSection, IEnumerable<Variant> variants)
    {
        _configurationSection = configurationSection;
        _variants = variants;
    }

    public void Configure(TOptions options)
    {
        var config = JsonElementConversion.MergeVariantConfigurations(_variants);
        config.GetSection(_configurationSection).Bind(options);
    }

    public void Dispose()
    {
    }
}
