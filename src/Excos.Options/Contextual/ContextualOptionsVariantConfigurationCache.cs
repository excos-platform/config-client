// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using Excos.Options.Abstractions.Data;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excos.Options.Contextual;

/// <summary>
/// Internal cache for variant-based configuration objects using LRU eviction.
/// </summary>
internal sealed class ContextualOptionsVariantConfigurationCache
{
    private readonly IOptionsMonitor<ExcosVariantConfigurationCacheOptions> _options;
    private readonly ConcurrentDictionary<ulong, CacheEntry> _cache = new();

    public ContextualOptionsVariantConfigurationCache(IOptionsMonitor<ExcosVariantConfigurationCacheOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Gets or adds a configuration for the specified variants.
    /// </summary>
    /// <param name="variants">The variants to get or create configuration for.</param>
    /// <returns>The cached or newly created configuration.</returns>
    public IConfiguration GetOrAdd(IEnumerable<Variant> variants)
    {
        var hash = variants.ComputeVariantHash();

        if (_cache.TryGetValue(hash, out var entry))
        {
            // Update last access time
            Interlocked.Exchange(ref entry.LastAccessTicks, DateTime.UtcNow.Ticks);
            return entry.Configuration;
        }

        // Cache miss - create new configuration
        var configuration = variants.ToConfiguration();
        var newEntry = new CacheEntry(configuration, DateTime.UtcNow.Ticks);

        // Check if we need to evict
        var maxSize = _options.CurrentValue.MaxCacheSize;
        if (_cache.Count >= maxSize)
        {
            EvictLeastRecentlyUsed();
        }

        _cache.TryAdd(hash, newEntry);
        return configuration;
    }

    private void EvictLeastRecentlyUsed()
    {
        ulong oldestKey = 0;
        long oldestTicks = long.MaxValue;

        foreach (var kvp in _cache)
        {
            var ticks = Interlocked.Read(ref kvp.Value.LastAccessTicks);
            if (ticks < oldestTicks)
            {
                oldestTicks = ticks;
                oldestKey = kvp.Key;
            }
        }

        if (oldestKey != 0)
        {
            _cache.TryRemove(oldestKey, out _);
        }
    }

    private sealed class CacheEntry
    {
        public IConfiguration Configuration { get; }
        public long LastAccessTicks;

        public CacheEntry(IConfiguration configuration, long lastAccessTicks)
        {
            Configuration = configuration;
            LastAccessTicks = lastAccessTicks;
        }
    }
}
