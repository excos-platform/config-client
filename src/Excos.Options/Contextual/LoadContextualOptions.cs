// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

internal class LoadContextualOptions<TOptions> : ILoadContextualOptions<TOptions>
    where TOptions : class
{
    private readonly string? _name;
    private readonly string _configurationSection;
    private readonly IEnumerable<IExperimentProvider> _experimentProviders;
    private readonly IEnumerable<IExperimentVariantOverride> _variantOverrides;

    public LoadContextualOptions(string? name, string configurationSection, IEnumerable<IExperimentProvider> experimentProviders, IEnumerable<IExperimentVariantOverride> variantOverrides)
    {
        _name = name;
        _configurationSection = configurationSection;
        _experimentProviders = experimentProviders;
        _variantOverrides = variantOverrides;
    }

    public ValueTask<IConfigureContextualOptions<TOptions>> LoadAsync<TContext>(string name, in TContext context, CancellationToken cancellationToken) where TContext : IOptionsContext
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);

        if (_name == null || name == _name)
        {
            return LoadExperimentAsync(context, cancellationToken);
        }

        return new ValueTask<IConfigureContextualOptions<TOptions>>(NullConfigureContextualOptions.GetInstance<TOptions>());
    }

    private async ValueTask<IConfigureContextualOptions<TOptions>> LoadExperimentAsync<TContext>(TContext context, CancellationToken cancellationToken) where TContext : IOptionsContext
    {
        var configure = new ConfigureContextualOptions<TOptions>(_configurationSection);

        var receiver = new ContextReceiver();
        context.PopulateReceiver(receiver);

        // only instantiate metadata if expected by options type
        var optionsMetadataPropertyName = TryGetMetadataPropertyName();
        ExperimentMetadata? metadataCollection = optionsMetadataPropertyName != null ? new() : null;

        foreach (var provider in _experimentProviders)
        {
            var experiments = await provider.GetExperimentsAsync(cancellationToken);

            var applicableExperiments = experiments.Where(e => receiver.Satisfies(e.Filters));
            foreach (var experiment in applicableExperiments)
            {
                var variantOverride = await TryGetVariantOverrideAsync(experiment, context, cancellationToken);

                if (variantOverride != null)
                {
                    var (variant, metadata) = variantOverride.Value;
                    configure.ConfigureOptions.Add(variant.Configuration);
                    metadataCollection?.Experiments.Add(new()
                    {
                        ExperimentName = experiment.Name,
                        ExperimentProvider = experiment.ProviderName,
                        VariantId = variant.Id,
                        IsOverridden = true,
                        OverrideProviderName = variantOverride.Value.metadata.OverrideProviderName,
                    });
                }
                else
                {
                    var allocationSpot = receiver.GetIdentifierAllocationSpot(experiment.Salt);
                    var matchingVariant = experiment.Variants
                        .Where(v => v.Allocation.Contains(allocationSpot))
                        .Where(v => receiver.Satisfies(v.Filters))
                        .OrderByDescending(v => v.Filters.Count) // the one with the most filters first
                        .OrderBy(v => v.Priority) // the one with lowest priority first (if specified) // TODO needs to be unit tested for null handling
                        .FirstOrDefault();

                    if (matchingVariant != null)
                    {
                        configure.ConfigureOptions.Add(matchingVariant.Configuration);
                        metadataCollection?.Experiments.Add(new()
                        {
                            ExperimentName = experiment.Name,
                            ExperimentProvider = experiment.ProviderName,
                            VariantId = matchingVariant.Id,
                        });
                    }
                }
            }
        }

        if (metadataCollection?.Experiments.Count > 0)
        {
            configure.ConfigureOptions.Add(new ConfigureExperimentMetadata(metadataCollection, optionsMetadataPropertyName!));
        }

        return configure;
    }

    private string? TryGetMetadataPropertyName()
    {
        foreach (var property in typeof(TOptions).GetProperties())
        {
            if (property.PropertyType == typeof(ExperimentMetadata))
            {
                return property.Name;
            }
        }

        return null;
    }

    private async Task<(Variant variant, VariantOverride metadata)?> TryGetVariantOverrideAsync<TContext>(Experiment experiment, TContext optionsContext, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        foreach (var @override in _variantOverrides)
        {
            var variantOverride = await @override.TryOverrideAsync(experiment, optionsContext, cancellationToken);
            if (variantOverride != null && experiment.Variants.TryGetValue(variantOverride.Id, out var selectedVariant))
            {
                return (selectedVariant, variantOverride);
            }
        }

        return null;
    }
}
