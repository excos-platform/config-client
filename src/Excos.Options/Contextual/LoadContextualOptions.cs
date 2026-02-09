// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Utils;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Contextual;

internal class LoadContextualOptions<TOptions> : ILoadContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string? _name;
    private readonly string _configurationSection;
    private readonly IFeatureEvaluation _featureEvaluation;
    private readonly ContextualOptionsVariantConfigurationCache _cache;

    public LoadContextualOptions(
        string? name,
        string configurationSection,
        IFeatureEvaluation featureEvaluation,
        ContextualOptionsVariantConfigurationCache cache)
    {
        _name = name;
        _configurationSection = configurationSection;
        _featureEvaluation = featureEvaluation;
        _cache = cache;
    }

    public ValueTask<IConfigureContextualOptions<TOptions>> LoadAsync<TContext>(string name, in TContext context, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);

        if (_name == null || name == _name)
        {
            return GetConfigurationForFeaturesAsync(context, cancellationToken);
        }

        return new ValueTask<IConfigureContextualOptions<TOptions>>(NullConfigureContextualOptions.GetInstance<TOptions>());
    }

    private async ValueTask<IConfigureContextualOptions<TOptions>> GetConfigurationForFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        var variants = await _featureEvaluation.EvaluateFeaturesAsync(context, cancellationToken).ConfigureAwait(false);
        var configuration = _cache.GetOrAdd(variants);
        var configurationSection = string.IsNullOrEmpty(_configurationSection) ? configuration : configuration.GetSection(_configurationSection);
        return new ConfigureContextualOptions<TOptions>(configurationSection);
    }
}
