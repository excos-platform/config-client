// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook;

/// <summary>
/// GrowthBook feature provider with integrated caching.
/// Implements <see cref="IDisposable"/> to properly clean up resources.
/// </summary>
internal class GrowthBookFeatureProvider : IFeatureProvider, IDisposable
{
    private readonly IOptionsMonitor<GrowthBookOptions> _options;
    private readonly GrowthBookApiCaller _apiCaller;
    private readonly ILogger<GrowthBookFeatureProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private List<Feature> _cachedFeatures = new();
    private DateTimeOffset? _cacheExpiration;
    private bool _disposed;

    public GrowthBookFeatureProvider(
        IOptionsMonitor<GrowthBookOptions> options,
        GrowthBookApiCaller apiCaller,
        ILogger<GrowthBookFeatureProvider> logger)
    {
        _options = options;
        _apiCaller = apiCaller;
        _logger = logger;

        // Kick off background cache load if configured
        if (options.CurrentValue.RequestFeaturesOnInitialization)
        {
            _ = RefreshCacheAsync(_cts.Token);
        }
    }

    private bool IsNotInitialized => _cacheExpiration is null;
    private bool IsExpired => DateTimeOffset.UtcNow > _cacheExpiration;

    public async ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Link the provided token with our disposal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        if (IsNotInitialized)
        {
            await RefreshCacheAsync(linkedCts.Token).ConfigureAwait(false);
        }
        else if (IsExpired)
        {
            // Fire and forget refresh for expired cache, but use disposal token
            _ = RefreshCacheAsync(_cts.Token);
        }

        return _cachedFeatures;
    }

    private async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Double-check after acquiring lock
            if (_cacheExpiration.HasValue && DateTimeOffset.UtcNow <= _cacheExpiration)
            {
                return;
            }

            var (updated, growthBookFeatures) = await _apiCaller.GetFeaturesAsync(cancellationToken).ConfigureAwait(false);

            if (!updated && _cachedFeatures.Count > 0)
            {
                // No changes from API and we have cached data, just extend expiration
                _cacheExpiration = DateTimeOffset.UtcNow + _options.CurrentValue.CacheDuration;
                return;
            }

            // Update a secondary cache to not disrupt any current consumer of the primary cache
            var features = new List<Feature>(_cachedFeatures.Count);
            features.AddRange(GrowthBookFeatureParser.ConvertFeaturesToExcos(growthBookFeatures));

            // Atomic swap
            _ = Interlocked.Exchange(ref _cachedFeatures, features);

            _cacheExpiration = DateTimeOffset.UtcNow + _options.CurrentValue.CacheDuration;

            _logger.LogInformation("Loaded GrowthBook features: {features}", _cachedFeatures.Select(static f => $"{f.Name}[{f.Count}]"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during disposal or caller cancellation - don't log as error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request features from GrowthBook");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _semaphore.Dispose();
        _cts.Dispose();
    }
}
