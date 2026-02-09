// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

internal partial class ConfigureContextualOptions<TOptions> : IConfigureContextualOptions<TOptions>
    where TOptions : class
{
    private readonly IConfiguration _configuration;

    public ConfigureContextualOptions(IConfiguration configuration)
    {
        // TODO: Replace with JSON deserialization for perf
        // once STJ supports Populate method.
        _configuration = configuration;
    }

    public void Configure(TOptions options)
    {
        _configuration.Bind(options);
    }

    public void Dispose()
    {
    }
}
