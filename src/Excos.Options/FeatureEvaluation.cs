// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Excos.Options.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.DependencyInjection;

namespace Excos.Options;

/// <summary>
/// Service collection extension method to register the feature evaluation service.
/// </summary>
public static class FeatureEvaluationExtensions
{
    /// <summary>
    /// Registers the feature evaluation service.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Input service collection for chaining.</returns>
    public static IServiceCollection AddExcosFeatureEvaluation(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureEvaluation, FeatureEvaluation>();
        return services;
    }



    /// <summary>
    /// Evaluates features for a given context and returns the constructed options object.
    /// </summary>
    /// <typeparam name="TOptions">Options type.</typeparam>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <param name="featureEvaluation">Feature evaluation strategy.</param>
    /// <param name="sectionName">The configuration section name corresponding to the path in config under which the options object should be resolved.</param>
    /// <param name="context">Context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An object configured using the evaluated features.</returns>
    public static async ValueTask<TOptions> EvaluateFeaturesAsync<TOptions, TContext>(this IFeatureEvaluation featureEvaluation, string sectionName, TContext context, CancellationToken cancellationToken)
        where TOptions : class, new()
        where TContext : IOptionsContext
    {
        var options = new TOptions();
        await foreach (var variant in featureEvaluation.EvaluateFeaturesAsync(context, cancellationToken).ConfigureAwait(false))
        {
            variant.Configuration.Configure(options, sectionName);
        }

        return options;
    }
}

internal class FeatureEvaluation : IFeatureEvaluation
{
    private readonly IEnumerable<IFeatureProvider> _featureProviders;

    public FeatureEvaluation(IEnumerable<IFeatureProvider> featureProviders)
    {
        _featureProviders = featureProviders;
    }

    public async IAsyncEnumerable<Variant> EvaluateFeaturesAsync<TContext>(TContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        foreach (var provider in _featureProviders)
        {
            var features = await provider.GetFeaturesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var feature in features)
            {
                Variant? matchingVariant = TryFindMatchingVariant(context, feature);
                if (matchingVariant != null)
                {
                    yield return matchingVariant;
                }
            }
        }
    }

    private static Variant? TryFindMatchingVariant<TContext>(TContext context, Feature feature)
        where TContext : IOptionsContext
    {
        var variants = new List<Variant>(feature);
        variants.Sort(PriorityComparer.Instance); // the one with lowest priority first (if specified)

        foreach (var variant in variants)
        {
            bool satisfied = true;
            foreach (var filter in variant.Filters)
            {
                if (!filter.IsSatisfiedBy(context))
                {
                    satisfied = false;
                    break;
                }
            }

            if (!satisfied)
            {
                continue;
            }

            return variant;
        }

        return null;
    }

    /// <summary>
    /// Comparer for priority values where nulls are always greater than values so in ascending order will be considered last 
    /// </summary>
    private class PriorityComparer : IComparer<Variant>
    {
        public static PriorityComparer Instance { get; } = new PriorityComparer();
        public int Compare(Variant? x, Variant? y)
        {
            if (x?.Priority == y?.Priority) return 0;
            if (x?.Priority == null) return 1;
            if (y?.Priority == null) return -1;
            return x.Priority.CompareTo(y.Priority);
        }
    }
}
