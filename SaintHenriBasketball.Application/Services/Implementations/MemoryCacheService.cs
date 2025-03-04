using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Collections.Concurrent;

namespace SaintHenriBasketball.Application.Services.Implementations
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly MemoryCacheEntryOptions _defaultCacheOptions;

        private long _hits;
        private long _misses;

        public long Hits => _hits;
        public long Misses => _misses;
        public double HitRatio => _hits + _misses == 0 ? 0 : (double)_hits / (_hits + _misses);
        // Track keys by prefix for efficient invalidation
        private readonly ConcurrentDictionary<string, HashSet<string>> _keysByPrefix = new();

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;

            // Default cache duration of 15 minutes
            _defaultCacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
        }

        public Task<T?> GetAsync<T>(string key)
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out T? cachedValue))
                {
                    Interlocked.Increment(ref _hits);
                    _logger.LogTrace("Cache hit for key: {Key}", key);
                    return Task.FromResult(cachedValue);
                }

                Interlocked.Increment(ref _misses);
                _logger.LogTrace("Cache miss for key: {Key}", key);
                return Task.FromResult(default(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving value from cache for key: {Key}", key);
                return Task.FromResult(default(T));
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            try
            {
                var options = new MemoryCacheEntryOptions();

                if (absoluteExpiration.HasValue)
                {
                    options.SetAbsoluteExpiration(absoluteExpiration.Value);
                }
                else if (slidingExpiration.HasValue)
                {
                    options.SetSlidingExpiration(slidingExpiration.Value);
                }
                else
                {
                    options = _defaultCacheOptions;
                }

                // Track cache entry to allow by-prefix removal
                options.RegisterPostEvictionCallback(OnPostEviction);

                _memoryCache.Set(key, value, options);
                _logger.LogTrace("Added to cache: {Key}", key);

                // Add key to appropriate prefixes
                TrackKeyByPrefix(key);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
                return Task.CompletedTask;
            }
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                _logger.LogTrace("Removed from cache: {Key}", key);

                // Clean up key from tracking
                UntrackKey(key);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing value from cache for key: {Key}", key);
                return Task.CompletedTask;
            }
        }

        public Task RemoveByPrefixAsync(string prefix)
        {
            try
            {
                if (_keysByPrefix.TryGetValue(prefix, out var keys))
                {
                    foreach (var key in keys.ToArray())
                    {
                        _memoryCache.Remove(key);
                        _logger.LogTrace("Removed from cache by prefix {Prefix}: {Key}", prefix, key);
                    }
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing values from cache by prefix: {Prefix}", prefix);
                return Task.CompletedTask;
            }
        }

        private void TrackKeyByPrefix(string key)
        {
            // Extract prefix (everything before the last colon)
            int lastColonIndex = key.LastIndexOf(':');
            if (lastColonIndex > 0)
            {
                string prefix = key.Substring(0, lastColonIndex);
                _keysByPrefix.AddOrUpdate(
                    prefix,
                    new HashSet<string> { key },
                    (_, hashSet) =>
                    {
                        hashSet.Add(key);
                        return hashSet;
                    });
            }
        }

        private void UntrackKey(string key)
        {
            int lastColonIndex = key.LastIndexOf(':');
            if (lastColonIndex > 0)
            {
                string prefix = key.Substring(0, lastColonIndex);
                if (_keysByPrefix.TryGetValue(prefix, out var keys))
                {
                    keys.Remove(key);
                    if (keys.Count == 0)
                    {
                        _keysByPrefix.TryRemove(prefix, out _);
                    }
                }
            }
        }

        private void OnPostEviction(object key, object? value, EvictionReason reason, object? state)
        {
            if (key is string keyString)
            {
                UntrackKey(keyString);
            }
        }
    }
}