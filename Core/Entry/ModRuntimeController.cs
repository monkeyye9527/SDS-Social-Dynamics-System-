using System;
using HarmonyLib;
using UnityEngine;
using ONIModPack.Core.Diagnostics;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    public static class ModRuntimeController
    {
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            try
            {
                PatchGameUpdate(harmony);
                PatchGameClock(harmony);
                ModLogger.Info("[ModRuntime] Runtime controller initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[ModRuntime] Failed to initialize: {ex.Message}");
            }
        }

        private static void PatchGameUpdate(HarmonyLib.Harmony harmony)
        {
            try
            {
                var gameType = AccessTools.TypeByName("Game");
                if (gameType == null)
                {
                    ModLogger.Warning("[ModRuntime] Game type not found - skipping update patch");
                    return;
                }

                var updateMethod = AccessTools.Method(gameType, "Update");
                if (updateMethod == null)
                {
                    ModLogger.Warning("[ModRuntime] Game.Update method not found");
                    return;
                }

                harmony.Patch(updateMethod,
                    postfix: new HarmonyMethod(typeof(ModRuntimeController), nameof(OnGameUpdate)));
                ModLogger.Info("[ModRuntime] Patched Game.Update");
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[ModRuntime] Failed to patch Game.Update: {ex.Message}");
            }
        }

        private static void PatchGameClock(HarmonyLib.Harmony harmony)
        {
            try
            {
                var gameClockType = AccessTools.TypeByName("GameClock");
                if (gameClockType == null)
                {
                    ModLogger.Warning("[ModRuntime] GameClock type not found - skipping cycle patch");
                    return;
                }

                var spawnMethod = AccessTools.Method(gameClockType, "OnSpawn");
                if (spawnMethod != null)
                {
                    harmony.Patch(spawnMethod,
                        postfix: new HarmonyMethod(typeof(ModRuntimeController), nameof(OnGameClockSpawn)));
                    ModLogger.Info("[ModRuntime] Patched GameClock.OnSpawn");
                }
                else
                {
                    ModLogger.Warning("[ModRuntime] GameClock.OnSpawn method not found - trying Awake");
                    var awakeMethod = AccessTools.Method(gameClockType, "Awake");
                    if (awakeMethod != null)
                    {
                        harmony.Patch(awakeMethod,
                            postfix: new HarmonyMethod(typeof(ModRuntimeController), nameof(OnGameClockSpawn)));
                        ModLogger.Info("[ModRuntime] Patched GameClock.Awake");
                    }
                    else
                    {
                        ModLogger.Warning("[ModRuntime] Neither OnSpawn nor Awake found on GameClock");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[ModRuntime] Failed to patch GameClock: {ex.Message}");
            }
        }

        public static void OnGameUpdate()
        {
            try
            {
                if (ServiceRegistry.TryGet<ISimulationScheduler>(out var simScheduler))
                {
                    simScheduler.Update(Time.deltaTime);
                }
                
                var scheduler = ServiceRegistry.TryGet<SystemScheduler>(out var sys) ? sys : null;
                scheduler?.UpdateAll(Time.deltaTime);
                
                DebugSystem.Update();

                if (DebugSystem.IsVisible)
                    DebugSystem.Draw();
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"[ModRuntime] Error in OnGameUpdate: {ex.Message}");
            }
        }

        public static void OnGameClockSpawn()
        {
            try
            {
                var gameInstanceType = AccessTools.TypeByName("Game");
                if (gameInstanceType == null)
                {
                    ModLogger.Warning("[ModRuntime] Game type not found for cycle subscription");
                    return;
                }

                var instanceProperty = gameInstanceType.GetProperty("Instance");
                if (instanceProperty == null)
                {
                    ModLogger.Warning("[ModRuntime] Game.Instance property not found");
                    return;
                }

                var gameInstance = instanceProperty.GetValue(null);
                if (gameInstance == null)
                {
                    ModLogger.Warning("[ModRuntime] Game.Instance is null");
                    return;
                }

                var subscribeMethod = gameInstanceType.GetMethod("Subscribe");
                if (subscribeMethod == null)
                {
                    ModLogger.Warning("[ModRuntime] Game.Subscribe method not found");
                    return;
                }

                subscribeMethod.Invoke(gameInstance, new object[] {
                    (int)ONIModPack.Core.Compat.GameHashesExtensions.CycleStart,
                    new System.Action<object>(_ => OnCycleStart())
                });
                ModLogger.Info("[ModRuntime] Subscribed to CycleStart event");
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[ModRuntime] Failed to subscribe to cycle events: {ex.Message}");
            }
        }

        private static void OnCycleStart()
        {
            try
            {
                if (ServiceRegistry.TryGet<ISimulationScheduler>(out var simScheduler))
                {
                    simScheduler.CycleUpdate();
                }
                
                var scheduler = ServiceRegistry.TryGet<SystemScheduler>(out var sys) ? sys : null;
                scheduler?.CycleUpdateAll();
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"[ModRuntime] Error in OnCycleStart: {ex.Message}");
            }
        }
    }
}
