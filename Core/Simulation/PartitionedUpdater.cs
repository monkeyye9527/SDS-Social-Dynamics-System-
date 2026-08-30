using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ONIModPack.Core;

namespace ONIModPack.Core.Simulation
{
    public interface IPartitionedSystem
    {
        string SystemName { get; }
        int UpdateInterval { get; set; }
        void UpdatePartition(int partitionId, float deltaTime);
        int GetPartitionCount();
        int GetEntityCount();
    }

    public class PartitionedUpdater : IPartitionedUpdater
    {
        private readonly Dictionary<string, IPartitionedSystem> _systems = new Dictionary<string, IPartitionedSystem>();
        private readonly Dictionary<string, int> _systemCounters = new Dictionary<string, int>();
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public void Initialize()
        {
            if (_isInitialized) return;
            
            EventBus.Subscribe<CycleChangedEvent>(OnCycleChanged);
            _isInitialized = true;
            ModLogger.Info("[PartitionedUpdater] Initialized");
        }

        public void Shutdown()
        {
            EventBus.Unsubscribe<CycleChangedEvent>(OnCycleChanged);
            _systems.Clear();
            _systemCounters.Clear();
            _isInitialized = false;
            ModLogger.Debug("[PartitionedUpdater] Shutdown");
        }

        public void RegisterSystem(IPartitionedSystem system)
        {
            if (system == null) return;
            
            _systems[system.SystemName] = system;
            _systemCounters[system.SystemName] = 0;
            ModLogger.Debug($"[PartitionedUpdater] Registered system: {system.SystemName}");
        }

        public void UnregisterSystem(string systemName)
        {
            _systems.Remove(systemName);
            _systemCounters.Remove(systemName);
            ModLogger.Debug($"[PartitionedUpdater] Unregistered system: {systemName}");
        }

        private void OnCycleChanged(CycleChangedEvent e)
        {
            foreach (var (name, system) in _systems)
            {
                _systemCounters[name]++;
                
                if (_systemCounters[name] >= system.UpdateInterval)
                {
                    _systemCounters[name] = 0;
                    UpdateSystem(system);
                }
            }
        }

        private void UpdateSystem(IPartitionedSystem system)
        {
            try
            {
                var partitionCount = system.GetPartitionCount();
                var entitiesPerPartition = Mathf.CeilToInt((float)system.GetEntityCount() / partitionCount);
                
                for (int i = 0; i < partitionCount; i++)
                {
                    system.UpdatePartition(i, 1f);
                }
                
                ModLogger.Debug($"[PartitionedUpdater] Updated {system.SystemName} - {system.GetEntityCount()} entities in {partitionCount} partitions");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[PartitionedUpdater] Error updating {system.SystemName}: {ex.Message}");
            }
        }

        public void ForceUpdate(string systemName)
        {
            if (_systems.TryGetValue(systemName, out var system))
            {
                UpdateSystem(system);
            }
        }

        public void ForceUpdateAll()
        {
            foreach (var system in _systems.Values)
            {
                UpdateSystem(system);
            }
        }

        public int GetSystemCount()
        {
            return _systems.Count;
        }

        public IPartitionedSystem GetSystem(string systemName)
        {
            return _systems.TryGetValue(systemName, out var system) ? system : null;
        }

        public List<string> GetAllSystemNames()
        {
            return new List<string>(_systems.Keys);
        }

        public IEnumerable<IPartitionedSystem> GetAllSystems()
        {
            return _systems.Values;
        }
    }

    public abstract class PartitionedSystemBase : IPartitionedSystem
    {
        public abstract string SystemName { get; }
        public int UpdateInterval { get; set; } = 10;
        
        protected readonly List<object> _entities = new List<object>();
        protected readonly int _partitionCount = 4;
        protected readonly object _lock = new object();

        public abstract void UpdatePartition(int partitionId, float deltaTime);

        public int GetPartitionCount()
        {
            return _partitionCount;
        }

        public int GetEntityCount()
        {
            lock (_lock)
            {
                return _entities.Count;
            }
        }

        protected int GetPartitionForEntity(object entity)
        {
            int hash = entity.GetHashCode();
            return Math.Abs(hash % _partitionCount);
        }

        protected List<object> GetEntitiesInPartition(int partitionId)
        {
            lock (_lock)
            {
                return _entities
                    .Where(e => GetPartitionForEntity(e) == partitionId)
                    .ToList();
            }
        }
    }

    public class EnvironmentPartitionedUpdater : PartitionedSystemBase
    {
        public override string SystemName => "Environment";

        public EnvironmentPartitionedUpdater()
        {
            UpdateInterval = 15;
        }

        public override void UpdatePartition(int partitionId, float deltaTime)
        {
            var entities = GetEntitiesInPartition(partitionId);
            
            foreach (var entity in entities)
            {
                UpdateEnvironmentEntity(entity, deltaTime);
            }
        }

        private void UpdateEnvironmentEntity(object entity, float deltaTime)
        {
        }

        public void AddEntity(object entity)
        {
            lock (_lock)
            {
                _entities.Add(entity);
            }
        }

        public void RemoveEntity(object entity)
        {
            lock (_lock)
            {
                _entities.Remove(entity);
            }
        }
    }

    public class SocialPartitionedUpdater : PartitionedSystemBase
    {
        public override string SystemName => "Social";

        public SocialPartitionedUpdater()
        {
            UpdateInterval = 20;
        }

        public override void UpdatePartition(int partitionId, float deltaTime)
        {
            var entities = GetEntitiesInPartition(partitionId);
            
            foreach (var entity in entities)
            {
                UpdateSocialEntity(entity, deltaTime);
            }
        }

        private void UpdateSocialEntity(object entity, float deltaTime)
        {
        }

        public void AddEntity(object entity)
        {
            lock (_lock)
            {
                _entities.Add(entity);
            }
        }

        public void RemoveEntity(object entity)
        {
            lock (_lock)
            {
                _entities.Remove(entity);
            }
        }
    }
}