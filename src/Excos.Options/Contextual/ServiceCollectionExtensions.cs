// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string section)
        where TOptions : class
    => services.ConfigureExcos<TOptions>(Microsoft.Extensions.Options.Options.DefaultName, section);

    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string name, string section)
        where TOptions : class
    {
        return services
            .AddContextualOptions()
            .AddSingleton<ILoadContextualOptions<TOptions>>(sp =>
                new LoadContextualOptions<TOptions>(
                    name,
                    section,
                    sp.GetServices<IExperimentProvider>(),
                    sp.GetServices<IExperimentVariantOverride>()));
    }
}
