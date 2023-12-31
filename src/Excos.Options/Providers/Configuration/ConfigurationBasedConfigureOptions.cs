// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excos.Options.Providers.Configuration;

/// <summary>
/// Wrapper over <see cref="IConfiguration"/> that performs binding to options later on.
/// </summary>
/// <remarks>
/// Instances of this class are not being pooled because they should be rather long lived (Options lifecycle).
/// </remarks> 
internal class ConfigurationBasedConfigureOptions : IConfigureOptions
{
    private readonly IConfiguration _configuration;

    public ConfigurationBasedConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure<TOptions>(TOptions input, string section) where TOptions : class
    {
        _configuration.GetSection(section).Bind(input);
    }
}
