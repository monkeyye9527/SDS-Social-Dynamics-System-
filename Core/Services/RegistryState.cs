using System;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core.Services
{
    public class RegistryState<TKey>
    {
        private readonly Dictionary<TKey, RegistrationEntry> _registry = new Dictionary<TKey, RegistrationEntry>();
        private readonly object _lock = new object();

        public int RegisteredCount => _registry.Count;

        public bool IsRegistered(TKey key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var entry))
                {
                    return !entry.IsUnregistered;
                }
                return false;
            }
        }

        public RegistrationStatus Register(TKey key, object data = null)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var existing))
                {
                    if (existing.IsUnregistered)
                    {
                        existing.IsUnregistered = false;
                        existing.RegisterCount++;
                        existing.LastRegisteredAt = System.DateTime.Now;
                        existing.Data = data;
                        ModLogger.Debug($"[RegistryState] Re-registered: {key} (count: {existing.RegisterCount})");
                        return RegistrationStatus.ReRegistered;
                    }
                    
                    existing.RegisterCount++;
                    ModLogger.Warning($"[RegistryState] Already registered: {key} (count: {existing.RegisterCount})");
                    return RegistrationStatus.AlreadyRegistered;
                }

                _registry[key] = new RegistrationEntry
                {
                    Key = key,
                    RegisterCount = 1,
                    LastRegisteredAt = System.DateTime.Now,
                    Data = data
                };

                ModLogger.Debug($"[RegistryState] Registered: {key}");
                return RegistrationStatus.Success;
            }
        }

        public bool Unregister(TKey key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var entry))
                {
                    entry.RegisterCount--;
                    
                    if (entry.RegisterCount <= 0)
                    {
                        entry.IsUnregistered = true;
                        ModLogger.Debug($"[RegistryState] Unregistered: {key}");
                        return true;
                    }
                    
                    ModLogger.Debug($"[RegistryState] Decremented registration count: {key} (count: {entry.RegisterCount})");
                    return false;
                }

                ModLogger.Warning($"[RegistryState] Not found for unregister: {key}");
                return false;
            }
        }

        public bool TryGetRegistration(TKey key, out RegistrationEntry entry)
        {
            lock (_lock)
            {
                return _registry.TryGetValue(key, out entry) && !entry.IsUnregistered;
            }
        }

        public object GetData(TKey key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var entry) && !entry.IsUnregistered)
                {
                    return entry.Data;
                }
                return null;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _registry.Clear();
                ModLogger.Debug("[RegistryState] Registry cleared");
            }
        }

        public IEnumerable<TKey> GetAllRegistered()
        {
            lock (_lock)
            {
                return _registry.Where(kvp => !kvp.Value.IsUnregistered)
                               .Select(kvp => kvp.Key);
            }
        }

        public int GetRegistrationCount(TKey key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var entry))
                {
                    return entry.RegisterCount;
                }
                return 0;
            }
        }

        public bool IsRegistrationCountExceeded(TKey key, int maxCount)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var entry))
                {
                    return entry.RegisterCount > maxCount;
                }
                return false;
            }
        }

        public enum RegistrationStatus
        {
            Success,
            AlreadyRegistered,
            ReRegistered
        }

        public class RegistrationEntry
        {
            public TKey Key { get; set; }
            public int RegisterCount { get; set; }
            public System.DateTime LastRegisteredAt { get; set; }
            public bool IsUnregistered { get; set; }
            public object Data { get; set; }
        }
    }

    public static class RegistryStateExtensions
    {
        public static bool EnsureRegistered<TKey>(this RegistryState<TKey> registry, TKey key, object data = null)
        {
            var status = registry.Register(key, data);
            return status == RegistryState<TKey>.RegistrationStatus.Success;
        }

        public static bool EnsureUnregistered<TKey>(this RegistryState<TKey> registry, TKey key)
        {
            return registry.Unregister(key);
        }
    }
}