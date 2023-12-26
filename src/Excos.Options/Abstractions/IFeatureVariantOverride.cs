// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;

using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Abstractions;

/// <summary>
/// A source of variant overrides.
/// </summary>
public interface IFeatureVariantOverride
{
    /// <summary>
    /// Checks if the variant for <paramref name="feature"/> has been overridden for the given context.
    /// </summary>
    /// <remarks>
    /// You can use this interface to implement override providers from various sources.
    /// For example in ASP.NET context there could be an override based on query parameters of the request.
    /// Or you can override the variant based on a list of test user ids.
    /// If there's multiple overrides registered, the first one which returns a value will be applied.
    /// </remarks> 
    /// <param name="feature">Experiment.</param>
    /// <param name="optionsContext">Context.</param>
    /// <returns>Variant override that should be used for the <paramref name="feature"/>, or <c>null</c> if no override is made.</returns>
    Task<VariantOverride?> TryOverrideAsync<TContext>(Feature feature, TContext optionsContext, CancellationToken cancellationToken)
        where TContext : IOptionsContext;
}
