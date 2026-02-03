// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
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

    public List<JsonElement> ConfigurationJsons { get; } = new(8);

    public void Configure(TOptions options)
    {
        foreach (var json in ConfigurationJsons)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(JsonElementConversion.ToConfigurationDictionary(json))
                .Build();
            config.GetSection(_configurationSection).Bind(options);
        }
    }

    public void Dispose()
    {
    }
}
