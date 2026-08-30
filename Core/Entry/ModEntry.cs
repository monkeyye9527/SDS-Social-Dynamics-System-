using HarmonyLib;
using Harmony = HarmonyLib.Harmony;
using KMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ONIModPack.Core.Config;
using ONIModPack.Core.Diagnostics;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    public enum ModState
    {
        Boot,
        Loading,
        Initialized,
        Running
    }
    
    public class ModEntry : UserMod2
    {
        public static HarmonyLib.Harmony HarmonyInstance { get; private set; }
        public static ModuleManager ModuleMgr { get; private set; }
        public static IConfigManager ConfigMgr { get; private set; }
        public static ModState CurrentState { get; private set; } = ModState.Boot;
        
        public static IReadOnlyList<IModModule> LoadedModules
        {
            get
            {
                EnsureReady();
                return ModuleMgr?.LoadedModules;
            }
        }

        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            base.OnLoad(harmony);
            HarmonyInstance = harmony;
            
            try
            {
                ONIModPack.Core.Logger.Info("========================================");
                ONIModPack.Core.Logger.Info("  ONI Mod Pack - Integrated v3.0");
                ONIModPack.Core.Logger.Info($"  Version: {Assembly.GetExecutingAssembly().GetName().Version}");
                ONIModPack.Core.Logger.Info($"  Harmony: {typeof(HarmonyLib.Harmony).Assembly.GetName().Version}");
                ONIModPack.Core.Logger.Info($"  Game: {BuildWatermark.GetBuildText()}");
                ONIModPack.Core.Logger.Info("========================================");

                SetState(ModState.Loading);
                
                RegisterBuildingStrings();
                
                AddBuildingsToPlanScreen();
                
                var configManager = new ConfigManager();
                configManager.Load();
                configManager.EnableHotReload();
                configManager.OnConfigChanged += OnConfigFileChanged;
                ConfigMgr = configManager;
                
                ModuleMgr = new ModuleManager(harmony);
                
                DiscoverAndRegisterModules();
                
                ApplyModuleConfigOverrides();
                
                if (!ModuleMgr.FinalizeRegistration())
                {
                    ONIModPack.Core.Logger.Error("Module registration finalization failed!");
                }
                
                DebugSystem.Initialize();
                
                ModRuntimeController.Initialize(harmony);
                
                ApplyPatches();
                RegisterOptions();
                
                ModuleLifecycleManager.Initialize(ModuleMgr);
                ModuleLifecycleManager.PublishLoad();
                
                SubscribeToApplicationEvents();
                
                SetState(ModState.Initialized);
                
                ONIModPack.Core.Logger.Info("ONI Mod Pack initialized successfully!");
                PrintModuleStatus();
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Error($"Failed to initialize ONI Mod Pack: {ex}");
                throw;
            }
        }
        
        private void SubscribeToApplicationEvents()
        {
            Application.quitting += OnApplicationQuitting;
            ONIModPack.Core.ModLogger.RegisterShutdownHandler();
            ONIModPack.Core.Logger.Debug("[ModEntry] Subscribed to Application.quitting event");
        }
        
        public static void EnsureReady()
        {
            if (CurrentState < ModState.Initialized)
            {
                throw new InvalidOperationException($"Mod not ready. Current state: {CurrentState}");
            }
        }
        
        private void SetState(ModState newState)
        {
            ONIModPack.Core.Logger.Debug($"[ModEntry] State transition: {CurrentState} -> {newState}");
            CurrentState = newState;
        }
        
        private void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;

            ONIModPack.Core.Logger.Info("========================================");
            ONIModPack.Core.Logger.Info("  ONI Mod Pack shutting down");
            ONIModPack.Core.Logger.Info("========================================");

            ONIModPack.Core.Logger.Info("ONI Mod Pack shutdown complete");

            ModuleMgr?.ShutdownAll();
            EventBus.Clear();
            ServiceRegistry.ShutdownAll();
            
            if (ConfigMgr != null)
            {
                ConfigMgr.Dispose();
                ConfigMgr = null;
            }
            
            HarmonyInstance?.UnpatchAll();
            ONIModPack.Core.ModLogger.Flush();
            ONIModPack.Core.ModLogger.Dispose();
        }

        public override void OnAllModsLoaded(HarmonyLib.Harmony harmony, IReadOnlyList<Mod> otherMods)
        {
            base.OnAllModsLoaded(harmony, otherMods);
            
            ONIModPack.Core.Logger.Info("All mods loaded, running compatibility checks...");
            
            var compatibilityResults = ModuleMgr.CheckCompatibility(otherMods);
            compatibilityResults.AddRange(ModConflictResolver.ResolveAll(ModuleMgr, otherMods));
            foreach (var result in compatibilityResults)
            {
                switch (result.Severity)
                {
                    case CompatibilitySeverity.Error:
                        ONIModPack.Core.Logger.Error($"[Compatibility] {result.Message}");
                        break;
                    case CompatibilitySeverity.Warning:
                        ONIModPack.Core.Logger.Warning($"[Compatibility] {result.Message}");
                        break;
                    case CompatibilitySeverity.Info:
                        ONIModPack.Core.Logger.Info($"[Compatibility] {result.Message}");
                        break;
                }
            }
            
            ModuleMgr.InitializeLoadedModules();
            ModuleMgr.StartLoadedModules();
        }

        private void DiscoverAndRegisterModules()
        {
            ONIModPack.Core.Logger.Debug("Discovering modules...");
            
            var moduleTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IModModule).IsAssignableFrom(t)
                         && !t.IsInterface
                         && !t.IsAbstract);
            
            var artificialWorldType = moduleTypes.FirstOrDefault(t => t.Name == "ArtificialWorldModule");
            if (artificialWorldType != null)
            {
                try
                {
                    var module = (IModModule)Activator.CreateInstance(artificialWorldType);
                    ModuleMgr.RegisterModule(module);
                    ONIModPack.Core.Logger.Debug($"  Discovered module: {module.ModuleId}");
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.Logger.Error($"Failed to create module instance for {artificialWorldType.Name}: {ex.Message}");
                }
            }
            
            foreach (var type in moduleTypes)
            {
                if (type.Name == "ArtificialWorldModule") continue;
                
                try
                {
                    var module = (IModModule)Activator.CreateInstance(type);
                    ModuleMgr.RegisterModule(module);
                    ONIModPack.Core.Logger.Debug($"  Discovered module: {module.ModuleId}");
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.Logger.Error($"Failed to create module instance for {type.Name}: {ex.Message}");
                }
            }
            
            ONIModPack.Core.Logger.Info($"Discovered {ModuleMgr.AllModules.Count} modules");
        }

        private void ApplyModuleConfigOverrides()
        {
            if (ConfigMgr == null) return;

            ONIModPack.Core.Logger.Debug("Applying module configuration overrides from global.yaml...");

            int enabledCount = 0;
            int disabledCount = 0;

            foreach (var module in ModuleMgr.AllModules)
            {
                string moduleId = module.ModuleId;

                bool configEnabled = ConfigMgr.IsModuleEnabled(moduleId, module.DefaultEnabled);

                if (!configEnabled && ModuleMgr.IsEnabled(moduleId))
                {
                    ModuleMgr.DisableModule(moduleId);
                    disabledCount++;
                    ONIModPack.Core.Logger.Info($"[Config] Disabled module: {moduleId}");
                }
                else if (configEnabled && !ModuleMgr.IsEnabled(moduleId))
                {
                    ModuleMgr.EnableModule(moduleId);
                    enabledCount++;
                    ONIModPack.Core.Logger.Info($"[Config] Enabled module: {moduleId}");
                }
            }

            ONIModPack.Core.Logger.Info($"[Config] Module configuration applied: {enabledCount} enabled, {disabledCount} disabled");
        }

        private void ApplyPatches()
        {
            ONIModPack.Core.Logger.Debug("Applying patches...");
            ModuleMgr.ApplyPatches();
            ONIModPack.Core.Logger.Info("Patches applied successfully");
        }

        private void RegisterOptions()
        {
            ONIModPack.Core.Logger.Debug("Registering options panel...");

            if (ModuleMgr == null) return;

            try
            {
                var optionsPanelType = AccessTools.TypeByName("OptionsPanel");
                if (optionsPanelType == null)
                {
                    optionsPanelType = AccessTools.TypeByName("PeterHan.PLib.OptionsPanel");
                }
                if (optionsPanelType == null)
                {
                    optionsPanelType = AccessTools.TypeByName("PLib.OptionsPanel");
                }

                object panelInstance = null;
                if (optionsPanelType != null)
                {
                    try
                    {
                        var instanceProp = optionsPanelType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (instanceProp != null)
                        {
                            panelInstance = instanceProp.GetValue(null);
                        }
                        else
                        {
                            var instanceField = optionsPanelType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                            if (instanceField != null)
                            {
                                panelInstance = instanceField.GetValue(null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Warning($"[ModEntry] Failed to get PLib.OptionsPanel instance: {ex.Message}");
                        panelInstance = null;
                    }
                }

                foreach (var module in ModuleMgr.LoadedModules)
                {
                    try
                    {
                        module.RegisterOptions(panelInstance);
                    }
                    catch (Exception ex)
                    {
                        ONIModPack.Core.Logger.Warning($"Failed to register options for {module.ModuleId}: {ex.Message}");
                    }
                }

                ONIModPack.Core.Logger.Info("Options panel registration complete");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Warning($"Options panel registration failed: {ex.Message}");
            }
        }

        private void OnConfigFileChanged(string moduleId)
        {
            ONIModPack.Core.Logger.Info($"Config updated for module: {moduleId}");
            if (moduleId == "global")
            {
                ONIModPack.Core.Logger.Info("Global configuration changes/reload effects will take effect on the next game start");
            }
        }

        private void PrintModuleStatus()
        {
            ONIModPack.Core.Logger.Info("=== Module Status ===");
            foreach (var module in ModuleMgr.AllModules)
            {
                var status = ModuleMgr.IsEnabled(module.ModuleId) ? "[ENABLED]" : "[DISABLED]";
                ONIModPack.Core.Logger.Info($"  {status} {module.ModuleId} ({module.DisplayName})");
            }
            ONIModPack.Core.Logger.Info("====================");
        }

        private void RegisterBuildingStrings()
        {
            try
            {
                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYCENTER.NAME", "社区中心");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYCENTER.DESC", "一个让殖民者聚集、社交和庆祝的场所。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYCENTER.EFFECT", "提供社交互动场所，增进殖民者之间的关系。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.UNIONHALL.NAME", "联合大厅");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.UNIONHALL.DESC", "大型集会场所，可容纳更多殖民者举行盛大活动。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.UNIONHALL.EFFECT", "举办大型活动，提升群体凝聚力。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.BROADCASTSTATION.NAME", "广播电台");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.BROADCASTSTATION.DESC", "向所有殖民者广播信息和公告。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.BROADCASTSTATION.EFFECT", "传播信息，影响殖民者情绪。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYGARDEN.NAME", "社区花园");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYGARDEN.DESC", "美化环境，提供放松和社交的绿色空间。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.COMMUNITYGARDEN.EFFECT", "增加装饰度，提供休闲场所。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.MEMORIALPLAZA.NAME", "纪念广场");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.MEMORIALPLAZA.DESC", "纪念逝去的殖民者，缅怀他们的贡献。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.MEMORIALPLAZA.EFFECT", "提升群体士气，纪念逝者。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.THINKTANK.NAME", "智囊团");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.THINKTANK.DESC", "促进知识交流和创新思考的场所。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.THINKTANK.EFFECT", "加速研究进度，激发创新思维。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.WELFARECENTER.NAME", "福利中心");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.WELFARECENTER.DESC", "为殖民者提供医疗和生活保障的设施。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.WELFARECENTER.EFFECT", "提供医疗服务，保障居民健康。");

                Strings.Add("STRINGS.BUILDINGS.PREFABS.SOLARPANELT2.NAME", "太阳能板T2");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.SOLARPANELT2.DESC", "更高效的太阳能发电设备。");
                Strings.Add("STRINGS.BUILDINGS.PREFABS.SOLARPANELT2.EFFECT", "高效太阳能发电，满足更大电力需求。");

                ONIModPack.Core.Logger.Info("[ModEntry] Building strings registered");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Warning($"[ModEntry] Failed to register building strings: {ex.Message}");
            }
        }

        private void AddBuildingsToPlanScreen()
        {
            try
            {
                ModUtil.AddBuildingToPlanScreen("Base", "CommunityCenter");
                ModUtil.AddBuildingToPlanScreen("Base", "UnionHall");
                ModUtil.AddBuildingToPlanScreen("Base", "BroadcastStation");
                ModUtil.AddBuildingToPlanScreen("Furniture", "CommunityGarden");
                ModUtil.AddBuildingToPlanScreen("Furniture", "MemorialPlaza");
                ModUtil.AddBuildingToPlanScreen("Base", "ThinkTank");
                ModUtil.AddBuildingToPlanScreen("Base", "WelfareCenter");
                ModUtil.AddBuildingToPlanScreen("Power", "SolarPanelT2");

                ONIModPack.Core.Logger.Info("[ModEntry] Buildings added to plan screen");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Warning($"[ModEntry] Failed to add buildings to plan screen: {ex.Message}");
            }
        }
    }
}