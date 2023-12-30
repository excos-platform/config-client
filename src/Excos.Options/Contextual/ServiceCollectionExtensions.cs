// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures an Excos <see cref="IContextualOptions"/> loader for the specified <typeparamref name="TOptions"/> type using the <paramref name="section"/> of configuration.
    /// </summary>
    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string section)
        where TOptions : class
    => services.ConfigureExcos<TOptions>(Microsoft.Extensions.Options.Options.DefaultName, section);

    /// <summary>
    /// Configures an Excos <see cref="IContextualOptions"/> loader for the specified named <typeparamref name="TOptions"/> type using the <paramref name="section"/> of configuration.
    /// </summary>
    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string name, string section)
        where TOptions : class
    {
        if (services.Any(s => s.ServiceType == typeof(IConfiguration)))
        {
            services.AddOptions<TOptions>(name).BindConfiguration(section);
        }

        return services
            .AddContextualOptions()
            .AddSingleton<ILoadContextualOptions<TOptions>>(sp =>
                new LoadContextualOptions<TOptions>(
                    name,
                    section,
                    sp.GetServices<IFeatureProvider>(),
                    sp.GetServices<IFeatureVariantOverride>(),
                    sp.GetRequiredService<IOptionsMonitor<ExcosOptions>>()));
    }
}
