using System;
using System.Collections.Generic;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    public enum SystemPhase
    {
        Input = 0,
        Logic = 1,
        Simulation = 2,
        State = 3,
        Presentation = 4,
        LateUpdate = 5
    }

    public enum SystemPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public interface IScheduledSystem
    {
        string SystemName { get; }
        bool IsEnabled { get; }
        void Update(float deltaTime);
    }

    public interface ICycleScheduledSystem : IScheduledSystem
    {
        void CycleUpdate();
    }

    public class ScheduledSystemAdapter : IScheduledSystem
    {
        private readonly System.Action<float> _updateAction;
        private readonly System.Func<bool> _isEnabledFunc;

        public string SystemName { get; }
        public bool IsEnabled => _isEnabledFunc?.Invoke() ?? true;

        public ScheduledSystemAdapter(string name, System.Action updateAction, System.Func<bool> isEnabledFunc = null)
        {
            SystemName = name;
            _updateAction = _ => updateAction();
            _isEnabledFunc = isEnabledFunc;
        }

        public ScheduledSystemAdapter(string name, System.Action<float> updateAction, System.Func<bool> isEnabledFunc = null)
        {
            SystemName = name;
            _updateAction = updateAction;
            _isEnabledFunc = isEnabledFunc;
        }

        public void Update(float deltaTime)
        {
            _updateAction?.Invoke(deltaTime);
        }
    }

    public class SystemScheduler : IService
    {
        private readonly Dictionary<SystemPhase, List<ScheduledSystemEntry>> _phaseSystems = 
            new Dictionary<SystemPhase, List<ScheduledSystemEntry>>();
        
        private readonly Dictionary<string, ScheduledSystemEntry> _systemLookup = 
            new Dictionary<string, ScheduledSystemEntry>();

        private float _lastDeltaTime;

        public bool IsInitialized { get; private set; }
        public int RegisteredCount => _systemLookup.Count;
        public float LastFrameTimeMs { get; private set; }

        public void Initialize()
        {
            foreach (SystemPhase phase in Enum.GetValues(typeof(SystemPhase)))
            {
                _phaseSystems[phase] = new List<ScheduledSystemEntry>();
            }

            IsInitialized = true;
            ModLogger.Info("[SystemScheduler] Initialized");
        }

        public void Shutdown()
        {
            _phaseSystems.Clear();
            _systemLookup.Clear();
            IsInitialized = false;
            ModLogger.Info("[SystemScheduler] Shutdown");
        }

        public void RegisterSystem(
            IScheduledSystem system,
            SystemPhase phase = SystemPhase.Logic,
            SystemPriority priority = SystemPriority.Normal,
            UpdateFrequency frequency = UpdateFrequency.EveryFrame)
        {
            if (system == null)
            {
                ModLogger.Error("[SystemScheduler] Cannot register null system");
                return;
            }

            string name = system.SystemName;
            if (_systemLookup.ContainsKey(name))
            {
                ModLogger.Warning($"[SystemScheduler] System '{name}' already registered, skipping");
                return;
            }

            var entry = new ScheduledSystemEntry
            {
                System = system,
                Phase = phase,
                Priority = priority,
                Frequency = frequency,
                Interval = GetInterval(frequency),
                Accumulator = 0f
            };

            _systemLookup[name] = entry;
            var phaseList = _phaseSystems[phase];
            phaseList.Add(entry);
            SortPhaseSystems(phaseList);

            ModLogger.Debug($"[SystemScheduler] Registered: {name} (phase={phase}, priority={priority}, freq={frequency})");
        }

        public void RegisterAction(
            string systemName,
            System.Action updateAction,
            SystemPhase phase = SystemPhase.Logic,
            SystemPriority priority = SystemPriority.Normal,
            UpdateFrequency frequency = UpdateFrequency.EveryFrame)
        {
            RegisterSystem(new ScheduledSystemAdapter(systemName, updateAction, null), phase, priority, frequency);
        }

        public void RegisterActionWithEnabled(
            string systemName,
            System.Action updateAction,
            System.Func<bool> isEnabledFunc,
            SystemPhase phase = SystemPhase.Logic,
            SystemPriority priority = SystemPriority.Normal,
            UpdateFrequency frequency = UpdateFrequency.EveryFrame)
        {
            RegisterSystem(new ScheduledSystemAdapter(systemName, updateAction, isEnabledFunc), phase, priority, frequency);
        }

        public void UnregisterSystem(string systemName)
        {
            if (_systemLookup.TryGetValue(systemName, out var entry))
            {
                if (_phaseSystems.TryGetValue(entry.Phase, out var list))
                {
                    list.Remove(entry);
                }
                _systemLookup.Remove(systemName);
                ModLogger.Debug($"[SystemScheduler] Unregistered: {systemName}");
            }
        }

        public void UpdateAll(float deltaTime)
        {
            if (!IsInitialized) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _lastDeltaTime = deltaTime;

            foreach (SystemPhase phase in Enum.GetValues(typeof(SystemPhase)))
            {
                if (_phaseSystems.TryGetValue(phase, out var systems))
                {
                    RunPhase(systems, deltaTime);
                }
            }

            sw.Stop();
            LastFrameTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        public void CycleUpdateAll()
        {
            if (!IsInitialized) return;

            foreach (var kvp in _systemLookup)
            {
                var entry = kvp.Value;
                if (entry.System.IsEnabled && entry.System is ICycleScheduledSystem cycleSys)
                {
                    try
                    {
                        cycleSys.CycleUpdate();
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"[SystemScheduler] CycleUpdate error in {entry.System.SystemName}: {ex}");
                    }
                }
            }
        }

        private void RunPhase(List<ScheduledSystemEntry> systems, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var entry = systems[i];
                if (!entry.System.IsEnabled) continue;

                if (entry.Frequency == UpdateFrequency.EveryFrame)
                {
                    ExecuteSystem(entry, deltaTime);
                }
                else
                {
                    entry.Accumulator += deltaTime;
                    while (entry.Accumulator >= entry.Interval)
                    {
                        entry.Accumulator -= entry.Interval;
                        ExecuteSystem(entry, entry.Interval);
                        if (!entry.System.IsEnabled) break;
                    }
                }
            }
        }

        private void ExecuteSystem(ScheduledSystemEntry entry, float deltaTime)
        {
            try
            {
                entry.FailureCount = 0;
                entry.System.Update(deltaTime);
            }
            catch (Exception ex)
            {
                entry.FailureCount++;
                ModLogger.Error($"[SystemScheduler] Error in {entry.System.SystemName}: {ex}");

                if (entry.FailureCount >= 5)
                {
                    ModLogger.Error($"[SystemScheduler] Disabling {entry.System.SystemName} after 5 failures");
                }
            }
        }

        private static float GetInterval(UpdateFrequency frequency)
        {
            switch (frequency)
            {
                case UpdateFrequency.EveryFrame: return 0f;
                case UpdateFrequency.Second1: return 1f;
                case UpdateFrequency.Second10: return 10f;
                case UpdateFrequency.Second30: return 30f;
                case UpdateFrequency.Minute1: return 60f;
                default: return 1f;
            }
        }

        private static void SortPhaseSystems(List<ScheduledSystemEntry> systems)
        {
            systems.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private class ScheduledSystemEntry
        {
            public IScheduledSystem System;
            public SystemPhase Phase;
            public SystemPriority Priority;
            public UpdateFrequency Frequency;
            public float Interval;
            public float Accumulator;
            public int FailureCount;
        }
    }
}
