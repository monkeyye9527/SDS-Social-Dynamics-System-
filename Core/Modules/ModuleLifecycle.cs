using System;
using System.Collections.Generic;

namespace ONIModPack.Core
{
    public interface IModModuleLifecycle
    {
        void OnLoad();
        void OnGameLoaded();
        void OnWorldLoaded();
        void OnSave();
        void OnUnload();
    }

    public static class ModuleLifecycleEvents
    {
        public class ModLoadEvent : GameEvent
        {
            public string Phase { get; set; }
        }

        public class GameLoadedEvent : GameEvent { }
        public class WorldLoadedEvent : GameEvent { }
        public class SaveEvent : GameEvent { }
        public class UnloadEvent : GameEvent { }
    }

    public static class ModuleLifecycleManager
    {
        private static readonly List<IModModuleLifecycle> _subscribers = new List<IModModuleLifecycle>();
        private static bool _initialized;

        public static void Initialize(ModuleManager moduleManager)
        {
            if (_initialized) return;
            _initialized = true;

            EventBus.Subscribe<ModuleLifecycleEvents.ModLoadEvent>(evt =>
                Dispatch(m => m.OnLoad(), "OnLoad"));
            EventBus.Subscribe<ModuleLifecycleEvents.GameLoadedEvent>(evt =>
                Dispatch(m => m.OnGameLoaded(), "OnGameLoaded"));
            EventBus.Subscribe<ModuleLifecycleEvents.WorldLoadedEvent>(evt =>
                Dispatch(m => m.OnWorldLoaded(), "OnWorldLoaded"));
            EventBus.Subscribe<ModuleLifecycleEvents.SaveEvent>(evt =>
                Dispatch(m => m.OnSave(), "OnSave"));
            EventBus.Subscribe<ModuleLifecycleEvents.UnloadEvent>(evt =>
                Dispatch(m => m.OnUnload(), "OnUnload"));

            if (moduleManager != null)
            {
                foreach (var module in moduleManager.LoadedModules)
                {
                    RegisterModule(module);
                }
            }

            ModLogger.Debug("[ModuleLifecycle] Manager initialized");
        }

        public static void RegisterModule(IModModule module)
        {
            if (module is IModModuleLifecycle lifecycle && !_subscribers.Contains(lifecycle))
            {
                _subscribers.Add(lifecycle);
            }
        }

        public static void RegisterLifecycle(IModModuleLifecycle lifecycle)
        {
            if (!_subscribers.Contains(lifecycle))
            {
                _subscribers.Add(lifecycle);
            }
        }

        public static void PublishLoad(string phase = "complete")
        {
            EventBus.Publish(new ModuleLifecycleEvents.ModLoadEvent { Phase = phase });
        }

        public static void PublishGameLoaded() =>
            EventBus.Publish(new ModuleLifecycleEvents.GameLoadedEvent());

        public static void PublishWorldLoaded() =>
            EventBus.Publish(new ModuleLifecycleEvents.WorldLoadedEvent());

        public static void PublishSave() =>
            EventBus.Publish(new ModuleLifecycleEvents.SaveEvent());

        public static void PublishUnload() =>
            EventBus.Publish(new ModuleLifecycleEvents.UnloadEvent());

        private static void Dispatch(Action<IModModuleLifecycle> action, string phase)
        {
            foreach (var subscriber in _subscribers)
            {
                try
                {
                    action(subscriber);
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[ModuleLifecycle] {phase} failed for {subscriber.GetType().Name}: {ex.Message}");
                }
            }
        }

        public static void Clear()
        {
            _subscribers.Clear();
            _initialized = false;
        }
    }
}
