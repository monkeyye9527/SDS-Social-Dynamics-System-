using System.Collections.Concurrent;

namespace ONIModPack.Core.Services
{
    public interface IBeliefCoreCache : IService
    {
        void Register(int instanceId, object core);
        void Unregister(int instanceId);
        T Get<T>(int instanceId) where T : class;
        void Clear();
    }

    public class BeliefCoreCacheService : ServiceBase, IBeliefCoreCache
    {
        public override string ServiceName => "BeliefCoreCache";

        private readonly ConcurrentDictionary<int, object> _cache = new ConcurrentDictionary<int, object>();

        public void Register(int instanceId, object core)
        {
            _cache[instanceId] = core;
        }

        public void Unregister(int instanceId)
        {
            _cache.TryRemove(instanceId, out _);
        }

        public T Get<T>(int instanceId) where T : class
        {
            if (_cache.TryGetValue(instanceId, out var core))
            {
                return core as T;
            }
            return null;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        protected override void OnShutdown()
        {
            Clear();
        }
    }
}