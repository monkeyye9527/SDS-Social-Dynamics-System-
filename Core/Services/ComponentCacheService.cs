using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace ONIModPack.Core.Services
{
    public interface IComponentCache : IService
    {
        T GetOrCache<T>(GameObject gameObject) where T : Component;
        T Get<T>(GameObject gameObject) where T : Component;
        void Clear();
        void Remove(GameObject gameObject);
        void RemoveAll(int instanceId);
    }

    public class ComponentCacheService : ServiceBase, IComponentCache
    {
        public override string ServiceName => "ComponentCache";

        private readonly ConcurrentDictionary<int, ConcurrentDictionary<Type, Component>> _cache =
            new ConcurrentDictionary<int, ConcurrentDictionary<Type, Component>>();

        public T GetOrCache<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null) return null;

            var instanceId = gameObject.GetInstanceID();
            var type = typeof(T);

            var objectCache = _cache.GetOrAdd(instanceId, _ => new ConcurrentDictionary<Type, Component>());

            if (objectCache.TryGetValue(type, out var cached))
            {
                return cached as T;
            }

            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                objectCache.TryAdd(type, component);
            }

            return component;
        }

        public T Get<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null) return null;

            var instanceId = gameObject.GetInstanceID();
            var type = typeof(T);

            if (_cache.TryGetValue(instanceId, out var objectCache) && objectCache.TryGetValue(type, out var component))
            {
                return component as T;
            }

            return null;
        }

        public void Clear()
        {
            _cache.Clear();
            ModLogger.Debug("[ComponentCache] Cache cleared");
        }

        public void Remove(GameObject gameObject)
        {
            if (gameObject == null) return;

            _cache.TryRemove(gameObject.GetInstanceID(), out _);
        }

        public void RemoveAll(int instanceId)
        {
            _cache.TryRemove(instanceId, out _);
        }

        protected override void OnShutdown()
        {
            Clear();
        }
    }
}