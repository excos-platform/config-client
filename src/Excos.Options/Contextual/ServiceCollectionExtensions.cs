// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

/// <summary>
/// Extension methods for adding Excos contextual options configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures an Excos <see cref="IContextualOptions{TOptions,TContext}"/> loader for the specified <typeparamref name="TOptions"/> type using the <paramref name="section"/> of configuration.
    /// </summary>
    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string section)
        where TOptions : class
    => services.ConfigureExcos<TOptions>(Microsoft.Extensions.Options.Options.DefaultName, section);

    /// <summary>
    /// Configures an Excos <see cref="IContextualOptions{TOptions,TContext}"/> loader for the specified named <typeparamref name="TOptions"/> type using the <paramref name="section"/> of configuration.
    /// </summary>
    public static IServiceCollection ConfigureExcos<TOptions>(this IServiceCollection services, string name, string section)
        where TOptions : class
    {
        if (services.Any(s => s.ServiceType == typeof(IConfiguration)))
        {
            services.AddOptions<TOptions>(name).BindConfiguration(section);
        }

        services.AddExcosFeatureEvaluation();

        return services
            .AddContextualOptions()
            .AddSingleton<ILoadContextualOptions<TOptions>>(sp =>
                new LoadContextualOptions<TOptions>(
                    name,
                    section,
                    sp.GetRequiredService<IFeatureEvaluation>()));
    }
}
