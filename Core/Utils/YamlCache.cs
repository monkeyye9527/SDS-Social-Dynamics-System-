using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ONIModPack.Core.Utils
{
    public static class YamlCache
    {
        private static readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private static readonly object _lock = new object();
        private static int _maxCacheSize = 100;
        private static TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public static void SetCacheSize(int maxSize)
        {
            _maxCacheSize = maxSize;
            CleanupIfNeeded();
        }

        public static void SetCacheDuration(TimeSpan duration)
        {
            _cacheDuration = duration;
        }

        public static T Get<T>(string path) where T : new()
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var entry))
                {
                    if (System.DateTime.Now - entry.CreatedAt < _cacheDuration)
                    {
                        ModLogger.Debug($"[YamlCache] Cache hit for {path}");
                        return (T)entry.Data;
                    }
                    else
                    {
                        ModLogger.Debug($"[YamlCache] Cache expired for {path}");
                        _cache.Remove(path);
                    }
                }
            }

            return default;
        }

        public static void Add<T>(string path, T data)
        {
            lock (_lock)
            {
                CleanupIfNeeded();

                _cache[path] = new CacheEntry
                {
                    Data = data,
                    CreatedAt = System.DateTime.Now,
                    FileLastWriteTime = File.Exists(path) ? File.GetLastWriteTime(path) : System.DateTime.Now
                };

                ModLogger.Debug($"[YamlCache] Added to cache: {path}");
            }
        }

        public static bool IsCached(string path)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var entry))
                {
                    return System.DateTime.Now - entry.CreatedAt < _cacheDuration;
                }
                return false;
            }
        }

        public static void Invalidate(string path)
        {
            lock (_lock)
            {
                if (_cache.Remove(path))
                {
                    ModLogger.Debug($"[YamlCache] Invalidated: {path}");
                }
            }
        }

        public static void InvalidateAll()
        {
            lock (_lock)
            {
                _cache.Clear();
                ModLogger.Debug("[YamlCache] All entries invalidated");
            }
        }

        public static void UpdateIfModified<T>(string path) where T : new()
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var entry))
                {
                    if (File.Exists(path))
                    {
                        var currentWriteTime = File.GetLastWriteTime(path);
                        if (currentWriteTime.Ticks > entry.FileLastWriteTime.Ticks)
                        {
                            ModLogger.Debug($"[YamlCache] File modified, reloading: {path}");
                            _cache.Remove(path);
                        }
                    }
                }
            }
        }

        private static void CleanupIfNeeded()
        {
            if (_cache.Count >= _maxCacheSize)
            {
                var oldestKeys = new List<string>();
                foreach (var kvp in _cache)
                {
                    if (System.DateTime.Now - kvp.Value.CreatedAt > _cacheDuration)
                    {
                        oldestKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in oldestKeys)
                {
                    _cache.Remove(key);
                }

                if (oldestKeys.Count == 0 && _cache.Count >= _maxCacheSize)
                {
                    var firstKey = _cache.Keys.GetEnumerator();
                    firstKey.MoveNext();
                    _cache.Remove(firstKey.Current);
                }
            }
        }

        private class CacheEntry
        {
            public object Data { get; set; }
            public System.DateTime CreatedAt { get; set; }
            public System.DateTime FileLastWriteTime { get; set; }
        }
    }
}