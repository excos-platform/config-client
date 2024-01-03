using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

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

    private static IEnumerable<Feature> ConvertFeaturesToExcos(Models.GrowthBookApiResponse? features)
    {
        if (features?.Features is null)
        {
            yield break;
        }

        foreach (var gbFeature in features.Features)
        {
            var defaultValue = gbFeature.Value.DefaultValue; // TODO: this should be used for a configuration provider as a base value for the options
            var feature = new Feature
            {
                Name = gbFeature.Key,
                ProviderName = ProviderName,
                Salt = gbFeature.Key,
            };

            var ruleIdx = 0;
            foreach (var rule in gbFeature.Value.Rules)
            {
                var namespaceId = rule.Namespace.ValueKind == JsonValueKind.Array ? rule.Namespace[0].GetString() : null;
                var namespaceRange = namespaceId is not null ? new Range<double>(rule.Namespace[1].GetDouble(), rule.Namespace[2].GetDouble(), RangeType.IncludeBoth) : (Range<double>?)null;

                var filters = ParseFilters(rule.Condition);
                if (namespaceId is not null)
                {
                    filters[rule.HashAttribute] = filters.TryGetValue(rule.HashAttribute, out var filter)
                        ? new NamespaceInclusiveFilter(namespaceId, namespaceRange!.Value, filter)
                        : new NamespaceInclusiveFilter(namespaceId, namespaceRange!.Value, null);
                }

                if (rule.Force.ValueKind != JsonValueKind.Undefined)
                {
                    var variant = new Variant
                    {
                        Id = $"_:{ruleIdx}",
                        Allocation = Allocation.Percentage(rule.Coverage),
                        Configuration = new JsonConfigure(gbFeature.Key, rule.Force),
                        Priority = ruleIdx,
                        AllocationUnit = rule.HashAttribute,
                        AllocationSalt = rule.Seed ?? rule.Key,
                        AllocationHash = rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                    };
                    variant.Filters.AddRange(filters.Select(kvp => new Filter { PropertyName = kvp.Key, Conditions = { kvp.Value } }));
                    feature.Variants.Add(variant);
                }
                else if (rule.Variations.ValueKind == JsonValueKind.Array && rule.Weights != null)
                {
                    var allocationRangeStart = 0.0;
                    for (var i = 0; i < rule.Variations.GetArrayLength(); i++)
                    {
                        var meta = rule.Meta?[i];
                        var variation = rule.Variations[i];
                        var allocation = new Range<double>(
                            Math.Max(0, allocationRangeStart * rule.Coverage),
                            Math.Min(1, (allocationRangeStart + rule.Weights[i]) * rule.Coverage),
                            RangeType.IncludeBoth);
                        allocationRangeStart += rule.Weights[i];

                        var variant = new Variant
                        {
                            Id = $"{rule.Key}:{meta?.Key ?? i.ToString()}",
                            Allocation = new Allocation(allocation),
                            Configuration = new JsonConfigure(gbFeature.Key, variation),
                            Priority = ruleIdx,
                            AllocationUnit = rule.HashAttribute,
                            AllocationSalt = rule.Seed ?? rule.Key,
                            AllocationHash = rule.HashVersion == 1 ? GrowthBookHash.V1 : GrowthBookHash.V2,
                        };
                        variant.Filters.AddRange(filters.Select(kvp => new Filter { PropertyName = kvp.Key, Conditions = { kvp.Value } }));
                        feature.Variants.Add(variant);
                    }
                }

                ruleIdx++;
            }

            yield return feature;
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

    private static Dictionary<string, IFilteringCondition> ParseFilters(JsonElement conditions)
    {
        var filters = new Dictionary<string, IFilteringCondition>();
        if (conditions.ValueKind == JsonValueKind.Object)
        {
            foreach (var condition in conditions.EnumerateObject())
            {
                var key = condition.Name;
                var value = condition.Value;
                filters.Add(key, ParseCondition(value));
            }
        }

        return filters;
    }

    private static IFilteringCondition ParseCondition(JsonElement condition)
    {
        if (condition.ValueKind == JsonValueKind.Object)
        {
            var properties = condition.EnumerateObject().ToList();
            if (properties.Count == 1)
            {
                return ParseConditionOperator(properties[0]);
            }
            else
            {
                return new AndFilter(properties.Select(ParseConditionOperator));
            }
        }
        else if (condition.ValueKind == JsonValueKind.Number)
        {
            return new ComparisonNumberFilter(r => r == 0, condition.GetDouble());
        }
        else if (condition.ValueKind == JsonValueKind.String)
        {
            return new ComparisonStringFilter(r => r == 0, condition.GetString()!);
        }
        else if (condition.ValueKind == JsonValueKind.Array)
        {
            var values = condition.EnumerateArray().Select(v => v.GetString()!).ToList();
            return new InFilter(values);
        }
        else if (condition.ValueKind == JsonValueKind.True)
        {
            return new ComparisonBoolFilter(r => r == 0, true);
        }
        else if (condition.ValueKind == JsonValueKind.False)
        {
            return new ComparisonBoolFilter(r => r == 0, false);
        }

        return NeverFilteringCondition.Instance;
    }

    private static IFilteringCondition ParseConditionOperator(JsonProperty property)
    {
        Version? version;
        if (property.Name == "$exists")
        {
            if (property.Value.ValueKind == JsonValueKind.False)
            {
                return new NotFilter(new ExistsFilter());
            }
            else if (property.Value.ValueKind == JsonValueKind.True)
            {
                return new ExistsFilter();
            }
        }
        else if (property.Name == "$not")
        {
            return new NotFilter(ParseCondition(property.Value));
        }
        else if (property.Name == "$and")
        {
            return new AndFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$or")
        {
            return new OrFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$nor")
        {
            return new NotFilter(new OrFilter(property.Value.EnumerateArray().Select(ParseCondition)));
        }
        else if (property.Name == "$in")
        {
            return new InFilter(property.Value.EnumerateArray().Select(v => v.GetString()!));
        }
        else if (property.Name == "$nin")
        {
            return new NotFilter(new InFilter(property.Value.EnumerateArray().Select(v => v.GetString()!)));
        }
        else if (property.Name == "$all")
        {
            return new AllFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$elemMatch")
        {
            return new ElemMatchFilter(ParseCondition(property.Value));
        }
        else if (property.Name == "$size")
        {
            return new SizeFilter(property.Value.GetInt32());
        }
        else if (property.Name == "$gt")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r > 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r > 0, property.Value.GetString()!);
        }
        else if (property.Name == "$gte")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r >= 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r >= 0, property.Value.GetString()!);
        }
        else if (property.Name == "$lt")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r < 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r < 0, property.Value.GetString()!);
        }
        else if (property.Name == "$lte")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r <= 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r <= 0, property.Value.GetString()!);
        }
        else if (property.Name == "$eq")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r == 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r == 0, property.Value.GetString()!);
        }
        else if (property.Name == "$ne")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r != 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r != 0, property.Value.GetString()!);
        }
        else if (property.Name == "$regex")
        {
            return new RegexFilteringCondition(property.Value.GetString()!);
        }
        else if (property.Name == "$vgt" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r > 0, version);
        }
        else if (property.Name == "$vgte" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r >= 0, version);
        }
        else if (property.Name == "$vlt" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r < 0, version); ;
        }
        else if (property.Name == "$vlte" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r <= 0, version);
        }
        else if (property.Name == "$veq" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r == 0, version);
        }
        else if (property.Name == "$vne" && Version.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r != 0, version);
        }

        return NeverFilteringCondition.Instance;
    }

    private class NamespaceInclusiveFilter : IFilteringCondition
    {
        private readonly string _namespaceId;
        private readonly Range<double> _range;
        private readonly IFilteringCondition? _innerCondition;

        public NamespaceInclusiveFilter(string namespaceId, Range<double> range, IFilteringCondition? innerCondition)
        {
            _namespaceId = namespaceId;
            _range = range;
            _innerCondition = innerCondition;
        }

        public bool IsSatisfiedBy<T>(T value)
        {
            var n = GrowthBookHash.V1.GetAllocationSpot($"__{_namespaceId}", value?.ToString() ?? string.Empty);
            return _range.Contains(n) && (_innerCondition?.IsSatisfiedBy(value) ?? true);
        }
    }

    private class GrowthBookHash : IAllocationHash
    {
        public static GrowthBookHash V1 { get; } = new GrowthBookHash(1);
        public static GrowthBookHash V2 { get; } = new GrowthBookHash(2);

        private readonly int _version;

        public GrowthBookHash(int version)
        {
            _version = version;
        }

        public double GetAllocationSpot(string salt, string identifier)
        {
            if (_version == 1)
            {
                uint n = FNV32A(identifier + salt);
                return (n % 1000) / 1000.0;
            }
            else if (_version == 2)
            {
                uint n = FNV32A(FNV32A(salt + identifier).ToString());
                return (n % 10000) / 10000;
            }

            return 0;
        }

        /// <summary>
        /// Implementation of the Fowler–Noll–Vo algorithm (fnv32a) algorithm.
        /// https://en.wikipedia.org/wiki/Fowler-Noll-Vo_hash_function
        /// </summary>
        /// <param name="value">The value to hash.</param>
        /// <returns>The hashed value.</returns>
        static uint FNV32A(string value)
        {
            uint hash = 0x811c9dc5;
            uint prime = 0x01000193;

            foreach (char c in value.ToCharArray())
            {
                hash ^= c;
                hash *= prime;
            }

            return hash;
        }
    }

    private class JsonConfigure : IConfigureOptions
    {
        private readonly IConfiguration _value;
        public JsonConfigure(string singleValueKey, JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                _value = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[] { KeyValuePair.Create(singleValueKey, value.GetString()) })
                    .Build();
            }
            else
            {
                _value = new ConfigurationBuilder()
                    .AddInMemoryCollection(JsonConfigurationFileParser.Parse(value))
                    .Build();
            }
        }
        public void Configure<TOptions>(TOptions input, string section) where TOptions : class
        {
            _value.GetSection(section).Bind(input);
        }
    }

    /// <summary>
    /// Copied from Microsoft.Extensions.Configuration.Json
    /// </summary>
    internal sealed class JsonConfigurationFileParser
    {
        private JsonConfigurationFileParser() { }

        private readonly Dictionary<string, string?> _data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _paths = new Stack<string>();

        public static IDictionary<string, string?> Parse(JsonElement input)
        {
            var parser = new JsonConfigurationFileParser();
            parser.VisitObjectElement(input);
            return parser._data;
        }

        private void VisitObjectElement(JsonElement element)
        {
            var isEmpty = true;

            foreach (JsonProperty property in element.EnumerateObject())
            {
                isEmpty = false;
                EnterContext(property.Name);
                VisitValue(property.Value);
                ExitContext();
            }

            SetNullIfElementIsEmpty(isEmpty);
        }

        private void VisitArrayElement(JsonElement element)
        {
            int index = 0;

            foreach (JsonElement arrayElement in element.EnumerateArray())
            {
                EnterContext(index.ToString());
                VisitValue(arrayElement);
                ExitContext();
                index++;
            }

            SetNullIfElementIsEmpty(isEmpty: index == 0);
        }

        private void SetNullIfElementIsEmpty(bool isEmpty)
        {
            if (isEmpty && _paths.Count > 0)
            {
                _data[_paths.Peek()] = null;
            }
        }

        private void VisitValue(JsonElement value)
        {
            Debug.Assert(_paths.Count > 0);

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObjectElement(value);
                    break;

                case JsonValueKind.Array:
                    VisitArrayElement(value);
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    string key = _paths.Peek();
                    if (_data.ContainsKey(key))
                    {
                        throw new FormatException($"Error_KeyIsDuplicated {key}");
                    }
                    _data[key] = value.ToString();
                    break;

                default:
                    throw new FormatException($"Error_KeyIsDuplicated {value.ValueKind}");
            }
        }

        private void EnterContext(string context) =>
            _paths.Push(_paths.Count > 0 ?
                _paths.Peek() + ConfigurationPath.KeyDelimiter + context :
                context);

        private void ExitContext() => _paths.Pop();
    }
}
