using HarmonyLib;
using KMod;
using System.Collections.Generic;
using ONIModPack.Core.Config;
using ONIModPack.Core.Diagnostics;
using ONIModPack.Core.Services;
using ONIModPack.Core.Assets;
using ONIModPack.Core.Debugging;
using ONIModPack.Core.Simulation;
using ONIModPack.Core.Modules;

namespace ONIModPack.Core
{
    public class CoreModule : ModModuleBase, IModModuleLifecycle
    {
        public CoreModule()
        {
            ModuleId = "Core";
        }
        
        public override string DisplayName => "Core";
        public override string Description => "Core module providing essential services";
        public override ModuleCategory Category => ModuleCategory.Core;
        public override string[] Dependencies => new string[0];
        public override bool DefaultEnabled => true;
        public override int Priority => ModulePriorities.System;

        public override CompatibilityResult CheckCompatibility(IReadOnlyList<KMod.Mod> loadedMods) =>
            CompatibilityResult.Ok();

        public override void RegisterPatches(HarmonyLib.Harmony harmony)
        {
            HarmonyPatchHelper.PatchPostfix(harmony, typeof(Game), "OnLoad",
                typeof(CoreModule), nameof(OnGameLoadPostfix));
            HarmonyPatchHelper.PatchPostfix(harmony, typeof(Game), "Load",
                typeof(CoreModule), nameof(OnWorldLoadPostfix));
            HarmonyPatchHelper.PatchPrefix(harmony, typeof(Game), "Save",
                typeof(CoreModule), nameof(OnSavePrefix));

            ModLogger.Debug("[CoreModule] Lifecycle patches registered");
        }

        public override void RegisterOptions(object panel) { }

        public override void Initialize()
        {
            ApplyLoggerConfig();
            
            var configManager = new ConfigManager();
            configManager.Initialize();
            configManager.EnableHotReload();
            ServiceRegistry.Register<IConfigManager>(configManager);
            
            var assetLoader = new AssetBundleLoader();
            assetLoader.Initialize();
            ServiceRegistry.Register<IAssetBundleLoader>(assetLoader);
            
            var devTools = new DevTools();
            devTools.Initialize();
            ServiceRegistry.Register<IDevTools>(devTools);
            
            var partitionedUpdater = new PartitionedUpdater();
            partitionedUpdater.Initialize();
            ServiceRegistry.Register<IPartitionedUpdater>(partitionedUpdater);
            
            var simulationScheduler = new SimulationScheduler();
            simulationScheduler.Initialize();
            ServiceRegistry.Register<ISimulationScheduler>(simulationScheduler);
            
            DevCommandRegistry.Initialize();
            
            var systemScheduler = new SystemScheduler();
            systemScheduler.Initialize();
            ServiceRegistry.Register<SystemScheduler>(systemScheduler);
            
            var saveDataManager = new SaveDataManager();
            saveDataManager.Initialize();
            ServiceRegistry.Register<SaveDataManager>(saveDataManager);
            ModuleLifecycleManager.RegisterLifecycle(saveDataManager);
            
            var gridAdapter = new GameGridAdapter();
            ServiceRegistry.Register<IGridAdapter>(gridAdapter);
            
            var componentCache = new ComponentCacheService();
            componentCache.Initialize();
            ServiceRegistry.Register<IComponentCache>(componentCache);
            
            var beliefCoreCache = new BeliefCoreCacheService();
            beliefCoreCache.Initialize();
            ServiceRegistry.Register<IBeliefCoreCache>(beliefCoreCache);
            
            ModLogger.Info("[CoreModule] Core module initialized");
        }

        public override void Start()
        {
            ModLogger.Debug("[CoreModule] Core module starting");
        }

        public override void Shutdown()
        {
            ServiceRegistry.Unregister<IBeliefCoreCache>();
            ServiceRegistry.Unregister<IComponentCache>();
            ServiceRegistry.Unregister<IGridAdapter>();
            ServiceRegistry.Unregister<IAssetBundleLoader>();
            ServiceRegistry.Unregister<IDevTools>();
            ServiceRegistry.Unregister<IPartitionedUpdater>();
            ModuleLifecycleManager.PublishUnload();
            ModLogger.Debug("[CoreModule] Core module shutting down");
        }

        public void OnLoad() =>
            ModLogger.Debug("[CoreModule] OnLoad lifecycle");

        public void OnGameLoaded() =>
            ModLogger.Debug("[CoreModule] OnGameLoaded lifecycle");

        public void OnWorldLoaded() =>
            ModLogger.Debug("[CoreModule] OnWorldLoaded lifecycle");

        public void OnSave() =>
            ModLogger.Debug("[CoreModule] OnSave lifecycle");

        public void OnUnload() => Shutdown();

        private static void ApplyLoggerConfig()
        {
            if (ModEntry.ConfigMgr == null) return;

            var level = ModEntry.ConfigMgr.GetConfigValue<string>("global", "logging.level", "Debug");
            if (System.Enum.TryParse<LogLevel>(level, true, out var parsed))
            {
                var maxMb = ModEntry.ConfigMgr.GetConfigValue<int>("global", "logging.maxFileSizeMb", 10);
                ModLogger.Configure(parsed, maxMb * 1024L * 1024L);
            }
        }

        private static void OnGameLoadPostfix()
        {
            ModuleLifecycleManager.PublishGameLoaded();
        }

        private static void OnWorldLoadPostfix()
        {
            ModuleLifecycleManager.PublishWorldLoaded();
        }

        private static void OnSavePrefix()
        {
            ModuleLifecycleManager.PublishSave();
        }
    }
}