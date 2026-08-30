﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using KMod;
using UnityEngine;
using ONIModPack.Core;
using ONIModPack.Core.Modules;

namespace ONIModPack.QoL
{
    public class SmartStorageModule : ModModuleBase
    {
        public SmartStorageModule()
        {
            ModuleId = "QoL.Convenience.SmartStorage";
        }
        
        public override string DisplayName => "Smart Storage System";
        public override string Description => "Improves storage priority system with dynamic priority adjustment based on fill rate";
        public override ModuleCategory Category => ModuleCategory.QoL;
        public override string[] Dependencies => new[] { "Core" };
        public override bool DefaultEnabled => true;
        public override int Priority => ModulePriorities.Feature;

        private static Dictionary<int, StorageInfo> _storageCache = new Dictionary<int, StorageInfo>();
        private static bool _enableDynamicPriority = true;
        private static float _highFillThreshold = 0.9f;
        private static float _lowFillThreshold = 0.1f;

        public static float GetLowFillThreshold() => _lowFillThreshold;
        public static float GetHighFillThreshold() => _highFillThreshold;

        public override CompatibilityResult CheckCompatibility(IReadOnlyList<Mod> loadedMods)
        {
            foreach (var mod in loadedMods)
            {
                if (mod.title.Contains("Storage") || mod.title.Contains("Priority"))
                {
                    return CompatibilityResult.Warning($"Potential conflict with mod: {mod.title}");
                }
            }
            return CompatibilityResult.Ok();
        }

        public override void RegisterPatches(Harmony harmony)
        {
            ModLogger.Info("[SmartStorage] Registering patches with HarmonyPriority");
            
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(FilteredStorage), "OnSpawn"),
                postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnFilteredStorageSpawn))
            );
            ModLogger.Info("[SmartStorage] Patched FilteredStorage.OnSpawn");
            
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Storage), "OnSpawn"),
                postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnStorageSpawn))
            );
            ModLogger.Info("[SmartStorage] Patched Storage.OnSpawn");
            
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Storage), "AddItem"),
                postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnItemAdded))
            );
            
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Storage), "RemoveItem"),
                postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnItemRemoved))
            );
            
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Storage), "OnCleanUp"),
                postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnStorageCleanUp))
            );
            
            var choreConsumerType = AccessTools.TypeByName("ChoreConsumer");
            if (choreConsumerType != null)
            {
                var getPriorityMethod = AccessTools.DeclaredMethod(choreConsumerType, "GetPriority");
                if (getPriorityMethod != null)
                {
                    harmony.Patch(
                        getPriorityMethod,
                        postfix: new HarmonyMethod(typeof(SmartStorageModule), nameof(OnGetChoreConsumerPriority))
                    );
                }
            }
        }

        public override void RegisterOptions(object panel)
        {
        }

        public override void Initialize()
        {
            ModLogger.Info($"[SmartStorage] Initialized (DynamicPriority: {_enableDynamicPriority})");
        }

        public override void Start()
        {
        }

        public override void Shutdown()
        {
            _storageCache.Clear();
            ModLogger.Info("[SmartStorage] Shutdown complete");
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnFilteredStorageSpawn(FilteredStorage __instance)
        {
            if (__instance == null) return;
            
            try
            {
                var storage = Traverse.Create(__instance).Field<Storage>("storage").Value;
                if (storage != null)
                {
                    int instanceId = storage.GetInstanceID();
                    _storageCache[instanceId] = new StorageInfo
                    {
                        BasePriority = GetStorageBasePriority(storage),
                        LastFillPercentage = GetFillPercentage(storage)
                    };
                    
                    ModLogger.Debug($"[SmartStorage] FilteredStorage spawned: {storage.name}");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[SmartStorage] Failed to process FilteredStorage spawn: {ex.Message}");
            }
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnStorageSpawn(Storage __instance)
        {
            if (__instance == null) return;
            
            try
            {
                int instanceId = __instance.GetInstanceID();
                if (!_storageCache.ContainsKey(instanceId))
                {
                    _storageCache[instanceId] = new StorageInfo
                    {
                        BasePriority = GetStorageBasePriority(__instance),
                        LastFillPercentage = GetFillPercentage(__instance)
                    };
                }
                
                ModLogger.Debug($"[SmartStorage] Storage spawned: {__instance.name}");
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[SmartStorage] Failed to process Storage spawn: {ex.Message}");
            }
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnItemAdded(Storage __instance, GameObject item)
        {
            if (__instance == null || !_enableDynamicPriority) return;
            
            try
            {
                UpdateStoragePriority(__instance);
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[SmartStorage] Failed to update priority on item add: {ex.Message}");
            }
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnItemRemoved(Storage __instance, GameObject item)
        {
            if (__instance == null || !_enableDynamicPriority) return;
            
            try
            {
                UpdateStoragePriority(__instance);
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[SmartStorage] Failed to update priority on item remove: {ex.Message}");
            }
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnStorageCleanUp(Storage __instance)
        {
            if (__instance == null) return;
            
            int instanceId = __instance.GetInstanceID();
            if (_storageCache.ContainsKey(instanceId))
            {
                _storageCache.Remove(instanceId);
                ModLogger.Debug($"[SmartStorage] Cache cleaned up for storage instance: {instanceId}");
            }
        }

        [HarmonyPriority(HarmonyLib.Priority.Last)]
        private static void OnGetChoreConsumerPriority(ref int __result, object __instance)
        {
            if (!_enableDynamicPriority || __instance == null) return;
            
            try
            {
                var go = __instance as Component;
                if (go != null)
                {
                    var storage = go.GetComponent<Storage>();
                    if (storage != null)
                    {
                        float fillPercentage = GetFillPercentage(storage);
                        int adjustedPriority = CalculateAdjustedPriority(fillPercentage, __result);
                        if (adjustedPriority != __result)
                        {
                            __result = adjustedPriority;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void UpdateStoragePriority(Storage storage)
        {
            int instanceId = storage.GetInstanceID();
            if (!_storageCache.TryGetValue(instanceId, out var info)) return;
            
            float currentFill = GetFillPercentage(storage);
            
            if (System.Math.Abs(currentFill - info.LastFillPercentage) < 0.05f) return;
            
            info.LastFillPercentage = currentFill;
            _storageCache[instanceId] = info;
            
            ModLogger.Debug($"[SmartStorage] {storage.name} fill updated: {currentFill:P1}");
        }

        internal static int CalculateAdjustedPriority(float fillPercentage, int basePriority)
        {
            if (fillPercentage >= _highFillThreshold)
            {
                return Mathf.Clamp(basePriority - 2, 1, 9);
            }
            else if (fillPercentage >= 0.7f)
            {
                return Mathf.Clamp(basePriority - 1, 1, 9);
            }
            else if (fillPercentage < _lowFillThreshold)
            {
                return Mathf.Clamp(basePriority + 2, 1, 9);
            }
            else if (fillPercentage < 0.3f)
            {
                return Mathf.Clamp(basePriority + 1, 1, 9);
            }
            
            return basePriority;
        }

        private static int GetStorageBasePriority(Storage storage)
        {
            if (storage == null) return 5;
            
            try
            {
                var prioritizable = storage.GetComponent<Prioritizable>();
                if (prioritizable != null)
                {
                    return (int)prioritizable.GetMasterPriority().priority_value;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"[SmartStorage] Failed to get base priority: {ex.Message}");
            }
            
            return 5;
        }

        private static float GetFillPercentage(Storage storage)
        {
            if (storage == null) return 0f;
            
            try
            {
                float current = storage.MassStored();
                float max = storage.capacityKg;
                
                if (max <= 0) return 0f;
                
                return Mathf.Clamp01(current / max);
            }
            catch
            {
                return 0f;
            }
        }
        
        public static void SetDynamicPriorityEnabled(bool enabled)
        {
            _enableDynamicPriority = enabled;
            ModLogger.Info($"[SmartStorage] Dynamic priority {(enabled ? "enabled" : "disabled")}");
        }
        
        public static void SetThresholds(float low, float high)
        {
            _lowFillThreshold = Mathf.Clamp01(low);
            _highFillThreshold = Mathf.Clamp01(high);
            ModLogger.Info($"[SmartStorage] Thresholds updated: low={_lowFillThreshold:P0}, high={_highFillThreshold:P0}");
        }

        private struct StorageInfo
        {
            public int BasePriority;
            public float LastFillPercentage;
        }
    }
}
