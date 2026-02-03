// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook;

/// <summary>
/// GrowthBook feature provider with integrated caching.
/// </summary>
internal class GrowthBookFeatureProvider : IFeatureProvider
{
    private readonly IOptionsMonitor<GrowthBookOptions> _options;
    private readonly GrowthBookApiCaller _apiCaller;
    private readonly ILogger<GrowthBookFeatureProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private List<Feature> _cachedFeatures = new();
    private DateTimeOffset? _cacheExpiration;

    public GrowthBookFeatureProvider(
        IOptionsMonitor<GrowthBookOptions> options,
        GrowthBookApiCaller apiCaller,
        ILogger<GrowthBookFeatureProvider> logger)
    {
        _options = options;
        _apiCaller = apiCaller;
        _logger = logger;
    }

    private bool IsNotInitialized => _cacheExpiration is null;
    private bool IsExpired => DateTimeOffset.UtcNow > _cacheExpiration;

    public async ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        if (IsNotInitialized)
        {
            await RefreshCacheAsync().ConfigureAwait(false);
        }
        else if (IsExpired)
        {
            // Fire and forget refresh for expired cache
            _ = RefreshCacheAsync();
        }

        return _cachedFeatures;
    }

    private async Task RefreshCacheAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            // Double-check after acquiring lock
            if (_cacheExpiration.HasValue && DateTimeOffset.UtcNow <= _cacheExpiration)
            {
                return;
            }

            var (updated, growthBookFeatures) = await _apiCaller.GetFeaturesAsync().ConfigureAwait(false);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request features from GrowthBook");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
