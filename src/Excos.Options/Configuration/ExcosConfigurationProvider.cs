// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Configuration;

/// <summary>
/// A configuration provider that loads feature variants and converts them to configuration key-value pairs.
/// Uses <see cref="IFeatureEvaluation"/> for variant selection.
/// </summary>
internal class ExcosConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IFeatureEvaluation _featureEvaluation;
    private readonly IOptionsContext _context;
    private readonly TimeSpan? _refreshPeriod;
    private readonly CancellationTokenSource _cts = new();
    private Timer? _refreshTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new configuration provider.
    /// </summary>
    /// <param name="featureEvaluation">Feature evaluation service for variant selection.</param>
    /// <param name="context">Context used for variant filtering.</param>
    /// <param name="refreshPeriod">Optional period for automatic refresh. If null, loads once.</param>
    public ExcosConfigurationProvider(
        IFeatureEvaluation featureEvaluation,
        IOptionsContext context,
        TimeSpan? refreshPeriod = null)
    {
        _featureEvaluation = featureEvaluation ?? throw new ArgumentNullException(nameof(featureEvaluation));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _refreshPeriod = refreshPeriod;
    }

    /// <inheritdoc/>
    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();

        if (_refreshPeriod.HasValue && _refreshTimer == null)
        {
            _refreshTimer = new Timer(
                _ => LoadAsync().GetAwaiter().GetResult(),
                null,
                _refreshPeriod.Value,
                _refreshPeriod.Value);
        }
    }

    private async Task LoadAsync()
    {
        var variants = await _featureEvaluation.EvaluateFeaturesAsync(_context, _cts.Token).ConfigureAwait(false);
        Data = variants.ToConfigurationDictionary();
        OnReload();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _refreshTimer?.Dispose();
        _cts.Dispose();
    }
}
