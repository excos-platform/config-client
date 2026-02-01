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
public class ExcosConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IFeatureProvider _featureProvider;
    private readonly DynamicContext _context;
    private readonly Timer _refreshTimer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ExcosConfigurationProvider class.
    /// </summary>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. Defaults to 15 minutes.</param>
    public ExcosConfigurationProvider(
        IDictionary<string, string> context,
        IFeatureProvider featureProvider,
        TimeSpan? refreshPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(featureProvider);

        _featureProvider = featureProvider;
        _context = new DynamicContext(context);
        
        var period = refreshPeriod ?? TimeSpan.FromMinutes(15);
        _refreshTimer = new Timer(OnRefreshTimer, null, period, period);
        
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
                // Consider adding logging here in production scenarios
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

            // Get all features
            var features = await _featureProvider.GetFeaturesAsync(CancellationToken.None).ConfigureAwait(false);
            
            // Filter variants based on context
            var matchedVariants = FilterVariantsByContext(features, _context);
            
            // Convert to configuration dictionary
            var configurations = matchedVariants.Select(v => v.Configuration);
            var data = VariantConfigurationUtilities.ToConfigurationDictionary(configurations);
            
            // Update data and trigger reload
            Data = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
            OnReload();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static List<Variant> FilterVariantsByContext(IEnumerable<Feature> features, DynamicContext context)
    {
        var matchedVariants = new List<Variant>();
        
        foreach (var feature in features)
        {
            // Find the first matching variant for each feature (respecting priority)
            var matchingVariant = feature
                .OrderBy(v => v.Priority)
                .FirstOrDefault(variant => AllFiltersMatch(variant, context));
            
            if (matchingVariant != null)
            {
                matchedVariants.Add(matchingVariant);
            }
        }
        
        return matchedVariants;
    }

    private static bool AllFiltersMatch(Variant variant, DynamicContext context)
    {
        foreach (var filter in variant.Filters)
        {
            if (!filter.IsSatisfiedBy(context))
            {
                return false;
            }
        }
        return true;
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
public class ExcosConfigurationSource : IConfigurationSource
{
    private readonly IDictionary<string, string> _context;
    private readonly IFeatureProvider _featureProvider;
    private readonly TimeSpan? _refreshPeriod;

    /// <summary>
    /// Initializes a new instance of the ExcosConfigurationSource class.
    /// </summary>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. Defaults to 15 minutes.</param>
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
    /// Adds the Excos dynamic context configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="context">Dictionary of context values for filtering variants.</param>
    /// <param name="featureProvider">Feature provider to fetch features from.</param>
    /// <param name="refreshPeriod">Period for refetching features. Defaults to 15 minutes.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddExcosDynamicContext(
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
