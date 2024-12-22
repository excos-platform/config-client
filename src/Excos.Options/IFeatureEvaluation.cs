// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options
{
    /// <summary>
    /// The main interface for feature evaluation of Excos.
    /// </summary>
    public interface IFeatureEvaluation
    {
        /// <summary>
        /// Evaluates features for a given context.
        /// </summary>
        /// <typeparam name="TContext">Context type.</typeparam>
        /// <param name="context">Context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Selected variants.</returns>
        IAsyncEnumerable<Variant> EvaluateFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken)
            where TContext : IOptionsContext;

        /// <summary>
        /// Evaluates features for a given context and returns the constructed options object.
        /// </summary>
        /// <typeparam name="TOptions">Options type.</typeparam>
        /// <typeparam name="TContext">Context type.</typeparam>
        /// <param name="sectionName">The configuration section name corresponding to the path in config under which the options object should be resolved.</param>
        /// <param name="context">Context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An object </returns>
        ValueTask<TOptions> EvaluateFeaturesAsync<TOptions, TContext>(string sectionName, TContext context, CancellationToken cancellationToken)
            where TOptions : class, new()
            where TContext : IOptionsContext;
    }
}
