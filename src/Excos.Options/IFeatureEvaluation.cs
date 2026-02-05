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
        ValueTask<IEnumerable<Variant>> EvaluateFeaturesAsync<TContext>(TContext context, CancellationToken cancellationToken)
            where TContext : IOptionsContext;
    }
}
