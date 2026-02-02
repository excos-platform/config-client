// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook
{
    internal class GrowthBookFeatureCache : BackgroundService
    {
        private readonly IOptionsMonitor<GrowthBookOptions> _options;
        private readonly GrowthBookApiCaller _growthBookApiCaller;
        private readonly ILogger<GrowthBookFeatureCache> _logger;
        private readonly GrowthBookDefaultValuesFeatureEvaluation? _defaultValuesEvaluation;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly System.Timers.Timer _configurationRefreshTimer;

        private List<Feature> _cachedFeatures = new();
        private DateTimeOffset? _cacheExpiration;

        public GrowthBookFeatureCache(
            IOptionsMonitor<GrowthBookOptions> options, 
            GrowthBookApiCaller growthBookApiCaller, 
            ILogger<GrowthBookFeatureCache> logger, 
            GrowthBookDefaultValuesFeatureEvaluation? defaultValuesEvaluation = null)
        {
            _options = options;
            _growthBookApiCaller = growthBookApiCaller;
            _logger = logger;
            _defaultValuesEvaluation = defaultValuesEvaluation;

            _configurationRefreshTimer = new System.Timers.Timer
            {
                AutoReset = true,
                Interval = options.CurrentValue.CacheDuration.TotalMilliseconds,
            };
            _configurationRefreshTimer.Elapsed += async (_, _) => await RequestFeaturesAsync().ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.CurrentValue.RequestFeaturesOnInitialization ||
                _defaultValuesEvaluation is not null)
            {
                await RequestFeaturesAsync().ConfigureAwait(false);
            }
        }

        private bool NeedsRefresh => IsNotInitialized || IsExpired;
        private bool IsNotInitialized => _cacheExpiration is null;
        private bool IsExpired => DateTimeOffset.UtcNow > _cacheExpiration;

        private async Task RequestFeaturesAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!NeedsRefresh)
                {
                    return;
                }

                var (updated, growthBookFeatures) = await _growthBookApiCaller.GetFeaturesAsync().ConfigureAwait(false);

                if (!updated)
                {
                    return; // no changes, no need to parse the data
                }

                // update a secondary cache to not disrupt any current consumer of the primary cache
                var features = new List<Feature>(_cachedFeatures.Count);
                features.AddRange(GrowthBookFeatureParser.ConvertFeaturesToExcos(growthBookFeatures));
                // then swap them
                _ = Interlocked.Exchange(ref _cachedFeatures, features);

                // Update the default values feature evaluation if registered
                _defaultValuesEvaluation?.UpdateFeatures(features);

                if (_defaultValuesEvaluation is not null && !_configurationRefreshTimer.Enabled)
                {
                    _configurationRefreshTimer.Start();
                }

                _cacheExpiration = DateTimeOffset.UtcNow + _options.CurrentValue.CacheDuration;

                _logger.LogInformation("Loaded the following GrowthBook features: {features}", _cachedFeatures.Select(static f => $"{f.Name}[${f.Count}]"));
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

        public async ValueTask<IEnumerable<Feature>> GetFeaturesAsync()
        {
            if (IsNotInitialized)
            {
                await RequestFeaturesAsync().ConfigureAwait(false);
            }
            else if (IsExpired)
            {
                _ = RequestFeaturesAsync();
            }

            return _cachedFeatures;
        }
    }
}
