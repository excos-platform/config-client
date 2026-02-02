// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Excos.Options.GrowthBook.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook
{
    internal class GrowthBookApiCaller
    {
        private static readonly JsonSerializerOptions GrowthBookJsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        private static readonly Dictionary<string, Feature> EmptyFeatures = new();

        private readonly IGrowthBookHttpClientProvider _httpClientProvider;
        private readonly ILogger<GrowthBookApiCaller> _logger;
        private readonly IOptionsMonitor<GrowthBookOptions> _options;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private IDictionary<string, Feature>? _growthBookFeatures;
        private DateTimeOffset? _lastUpdated;

        public GrowthBookApiCaller(IGrowthBookHttpClientProvider httpClientProvider, ILogger<GrowthBookApiCaller> logger, IOptionsMonitor<GrowthBookOptions> options)
        {
            _httpClientProvider = httpClientProvider;
            _logger = logger;
            _options = options;
        }

        public async Task<(bool updated, IDictionary<string, Feature> features)> GetFeaturesAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                var options = _options.CurrentValue;
                using var httpClient = _httpClientProvider.GetHttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.ApiHost, $"/api/features/{options.ClientKey}"));
                var response = await httpClient.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch features from GrowthBook API. Status code: {StatusCode}", response.StatusCode);
                    return (false, _growthBookFeatures ?? EmptyFeatures);
                }

                var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var responseJson = await JsonDocument.ParseAsync(responseStream).ConfigureAwait(false);

                // check if it has changed since the last fetch, if not, return the cached features
                var dateUpdated = responseJson.RootElement.GetProperty("dateUpdated").GetDateTimeOffset();
                if (_lastUpdated.HasValue && _lastUpdated.Value >= dateUpdated)
                {
                    _logger.LogDebug("GrowthBook API indicates there has been no update since {dateUpdated}", dateUpdated);
                    return (false, _growthBookFeatures ?? EmptyFeatures);
                }

                var apiResponse = responseJson.Deserialize<GrowthBookApiResponse>(GrowthBookJsonSerializerOptions);
                if (apiResponse?.Features is null)
                {
                    _logger.LogError("Failed to deserialize features from GrowthBook API");
                    return (false, _growthBookFeatures ?? EmptyFeatures);
                }

                _growthBookFeatures = apiResponse.Features;
                _lastUpdated = apiResponse.DateUpdated;

                _logger.LogInformation("Fetched new features from GrowthBook API - updated at {dateUpdated}", dateUpdated);

                return (true, _growthBookFeatures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch features from GrowthBook API");
                return (false, _growthBookFeatures ?? EmptyFeatures);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
