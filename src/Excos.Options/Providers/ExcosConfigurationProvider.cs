// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.Primitives;

namespace Excos.Options.Providers;

/// <summary>
/// Configuration provider that periodically refetches features based on a dynamic context
/// and updates its internal configuration dictionary.
/// </summary>
internal class ExcosConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IFeatureProvider _featureProvider;
    private readonly IFeatureEvaluation _featureEvaluation;
    private readonly DynamicContext _context;
    private readonly Timer? _refreshTimer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ExcosConfigurationProvider class.
    /// </summary>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. If null, configuration is loaded only once.</param>
    public ExcosConfigurationProvider(
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(featureProvider);

        _featureProvider = featureProvider;
        _featureEvaluation = new FeatureEvaluation(new[] { featureProvider });
        _context = new DynamicContext(context);
        
        // If refreshPeriod is provided, set up periodic refresh; otherwise, load once
        if (refreshPeriod.HasValue)
        {
            _refreshTimer = new Timer(OnRefreshTimer, null, refreshPeriod.Value, refreshPeriod.Value);
        }
        
        // Perform initial load synchronously
        RefreshAsync().GetAwaiter().GetResult();
    }

    private async void OnRefreshTimer(object? state)
    {
        if (!_disposed)
        {
            try
            {
                await RefreshAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Suppress exceptions from async void method to prevent application crashes
            }
        }
    }

    private async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        
        try
        {
            if (_disposed)
            {
                return;
            }

            // Use FeatureEvaluation to get matching variants
            var matchedVariants = new List<Variant>();
            await foreach (var variant in _featureEvaluation.EvaluateFeaturesAsync(_context, CancellationToken.None).ConfigureAwait(false))
            {
                matchedVariants.Add(variant);
            }
            
            // Convert to configuration dictionary
            var data = VariantConfigurationUtilities.ToConfigurationDictionary(matchedVariants);
            
            // Update data and trigger reload (ensure case-insensitive comparison)
            Data = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
            OnReload();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disposes the configuration provider and its resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _refreshTimer?.Dispose();
            _semaphore?.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration source for ExcosConfigurationProvider.
/// </summary>
internal class ExcosConfigurationSource : IConfigurationSource
{
    private readonly IDictionary<string, string> _context;
    private readonly IFeatureProvider _featureProvider;
    private readonly TimeSpan? _refreshPeriod;

    /// <summary>
    /// Initializes a new instance of the ExcosConfigurationSource class.
    /// </summary>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. If null, configuration is loaded only once.</param>
    public ExcosConfigurationSource(
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _featureProvider = featureProvider ?? throw new ArgumentNullException(nameof(featureProvider));
        _refreshPeriod = refreshPeriod;
    }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ExcosConfigurationProvider(_context, _featureProvider, _refreshPeriod);
    }
}

/// <summary>
/// Extension methods for adding ExcosConfigurationProvider to IConfigurationBuilder.
/// </summary>
public static class ExcosConfigurationExtensions
{
    /// <summary>
    /// Adds the Excos configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. If null, configuration is loaded only once.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddExcosConfiguration(
        this IConfigurationBuilder builder,
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        return builder.Add(new ExcosConfigurationSource(context, featureProvider, refreshPeriod));
    }
}

/// <summary>
/// Dynamic context implementation that wraps a dictionary of string values.
/// </summary>
internal class DynamicContext : IOptionsContext
{
    private readonly IDictionary<string, string> _values;

    public DynamicContext(IDictionary<string, string> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    /// <summary>
    /// Populates a receiver with all context values.
    /// </summary>
    /// <typeparam name="T">The receiver type.</typeparam>
    /// <param name="receiver">The receiver to populate.</param>
    void IOptionsContext.PopulateReceiver<T>(T receiver)
    {
        foreach (var kvp in _values)
        {
            receiver.Receive(kvp.Key, kvp.Value);
        }
    }
}
