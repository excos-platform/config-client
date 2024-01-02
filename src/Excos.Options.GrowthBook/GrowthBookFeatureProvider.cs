using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excos.Options.GrowthBook;

internal class GrowthBookFeatureProvider : IFeatureProvider
{
    public const string ProviderName = nameof(GrowthBook);

    private readonly IOptionsMonitor<GrowthBookOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GrowthBookFeatureProvider> _logger;
    
    private List<Feature> _cachedFeatures = new();
    private List<Feature> _secondaryCachedFeatures = new();
    private DateTimeOffset? _cacheExpiration;
    private Task? _requestFeaturesTask;

    public GrowthBookFeatureProvider(IOptionsMonitor<GrowthBookOptions> options, IHttpClientFactory httpClientFactory, ILogger<GrowthBookFeatureProvider> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        if (_options.CurrentValue.RequestFeaturesOnInitialization)
        {
            _ = RequestFeaturesAsync();
        }
    }

    private async Task RequestFeaturesAsync()
    {
        if (_requestFeaturesTask is not null)
        {
            await _requestFeaturesTask;
            return;
        }

        var taskSource = new TaskCompletionSource();
        var completionTask = taskSource.Task;

        // check if another thread already started the request
        if (null != Interlocked.CompareExchange(ref _requestFeaturesTask, completionTask, null))
        {
            await _requestFeaturesTask;
            return;
        }

        try
        {
            var options = _options.CurrentValue;
            var httpClient = _httpClientFactory.CreateClient(ProviderName);
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.ApiHost, $"/api/features/{options.ClientKey}"));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var features = JsonSerializer.Deserialize<Models.GrowthBookApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            // update the secondary cache to not disrupt any current consumer of the primary cache
            _secondaryCachedFeatures.Clear();
            _secondaryCachedFeatures.AddRange(ConvertFeaturesToExcos(features));
            // then swap them
            _secondaryCachedFeatures = Interlocked.Exchange(ref _cachedFeatures, _secondaryCachedFeatures);
            
            _cacheExpiration = DateTimeOffset.UtcNow + options.CacheDuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request features from GrowthBook");
        }
        finally
        {
            taskSource.SetResult();
            _requestFeaturesTask = null;
        }
    }

    private IEnumerable<Feature> ConvertFeaturesToExcos(Models.GrowthBookApiResponse? features)
    {
        if (features?.Features is null)
        {
            yield break;
        }

        foreach (var gbFeature in features.Features)
        {
            var defaultValue = gbFeature.Value.DefaultValue; // TODO: this should be used for a configuration provider as a base value for the options
            var ruleIdx = 0;
            foreach (var rule in gbFeature.Value.Rules)
            {
                var namespaceId = rule.Namespace.ValueKind == JsonValueKind.Array ? rule.Namespace[0].GetString() : null;
                var namespaceRange = namespaceId is not null ? new Range<double>(rule.Namespace[1].GetDouble(), rule.Namespace[2].GetDouble(), RangeType.IncludeBoth) : (Range<double>?)null;
                var feature = new Feature
                {
                    Name = rule.Key ?? gbFeature.Key,
                    ProviderName = ProviderName,
                    AllocationUnit = rule.HashAttribute,
                    Salt = rule.Seed ?? rule.Key ?? gbFeature.Key,
                };

                if (rule.Force.ValueKind != JsonValueKind.Undefined)
                {
                    feature.Variants.Add(new Variant
                    {
                        Id = $"Rule_{ruleIdx}",
                        Allocation = Allocation.Percentage(rule.Coverage),
                        Configuration = null!, //TODO based on rule.Force
                    });
                }
                else
                {
                    var variants = rule.Meta?.Select((v,i) => new Variant
                    {
                        Id = v.Key ?? i.ToString(),
                        Allocation = Allocation.Percentage(0), //rule.Ranges[i], //TODO
                        Configuration = null!, //TODO based on rule.Variations
                    });
                    
                }
                // parse filters

                ruleIdx++;
            }
        }
    }

    public async ValueTask<IEnumerable<Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        if (_cacheExpiration is null)
        {
            await RequestFeaturesAsync();
        }
        else if (DateTimeOffset.UtcNow > _cacheExpiration)
        {
            // request features in the background
            _ = RequestFeaturesAsync();
        }

        return _cachedFeatures;
    }

}
