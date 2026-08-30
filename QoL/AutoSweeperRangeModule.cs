﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using HarmonyLib;
using KMod;
using System.Collections.Generic;
using UnityEngine;
using ONIModPack.Core;
using ONIModPack.Core.Modules;

namespace ONIModPack.QoL
{
    public class AutoSweeperRangeModule : ModModuleBase
    {
        public AutoSweeperRangeModule()
        {
            ModuleId = "QoL.Automation.AutoSweeperRange";
        }
        
        public override string DisplayName => "Auto Sweeper Range Extension";
        public override string Description => "Extends the auto sweeper pickup range with configurable radius";
        public override ModuleCategory Category => ModuleCategory.QoL;
        public override string[] Dependencies => new[] { "Core" };
        public override bool DefaultEnabled => true;
        public override int Priority => ModulePriorities.Feature;

        public static float ExtendedRange = 4f;

        public override CompatibilityResult CheckCompatibility(IReadOnlyList<Mod> loadedMods)
        {
            foreach (var mod in loadedMods)
            {
                if (mod.title.Contains("SweepBot") || mod.title.Contains("AutoSweeper"))
                {
                    return CompatibilityResult.Warning($"Potential conflict with mod: {mod.title}");
                }
            }
            return CompatibilityResult.Ok();
        }

        public override void RegisterPatches(Harmony harmony)
        {
            ModLogger.Info("[AutoSweeperRange] Registering patches with HarmonyPriority");
            
            var sweeperConfigType = AccessTools.TypeByName("SweepBotConfig");
            if (sweeperConfigType != null)
            {
                var createBuildingDefMethod = AccessTools.DeclaredMethod(sweeperConfigType, "CreateBuildingDef");
                if (createBuildingDefMethod != null)
                {
                    harmony.Patch(
                        createBuildingDefMethod,
                        postfix: new HarmonyMethod(typeof(AutoSweeperRangeModule), nameof(AdjustSweepBotBuildingDef))
                    );
                    ModLogger.Info("[AutoSweeperRange] Patched SweepBotConfig.CreateBuildingDef");
                }
            }
            
            var sweepBotType = AccessTools.TypeByName("SweepBot");
            if (sweepBotType != null)
            {
                var getPickupRadiusMethod = AccessTools.DeclaredMethod(sweepBotType, "GetPickupRadius");
                if (getPickupRadiusMethod != null)
                {
                    harmony.Patch(
                        getPickupRadiusMethod,
                        postfix: new HarmonyMethod(typeof(AutoSweeperRangeModule), nameof(AdjustPickupRadius))
                    );
                    ModLogger.Info("[AutoSweeperRange] Patched SweepBot.GetPickupRadius");
                }
                
                var getOperationalRadiusMethod = AccessTools.DeclaredMethod(sweepBotType, "GetOperationalRadius");
                if (getOperationalRadiusMethod != null)
                {
                    harmony.Patch(
                        getOperationalRadiusMethod,
                        postfix: new HarmonyMethod(typeof(AutoSweeperRangeModule), nameof(AdjustOperationalRadius))
                    );
                    ModLogger.Info("[AutoSweeperRange] Patched SweepBot.GetOperationalRadius");
                }
                
                var detectMethod = AccessTools.DeclaredMethod(sweepBotType, "DetectPickupable");
                if (detectMethod != null)
                {
                    harmony.Patch(
                        detectMethod,
                        prefix: new HarmonyMethod(typeof(AutoSweeperRangeModule), nameof(PreDetectPickupable))
                    );
                    ModLogger.Info("[AutoSweeperRange] Patched SweepBot.DetectPickupable");
                }
            }
        }

        public override void RegisterOptions(object panel)
        {
        }

        public override void Initialize()
        {
            ModLogger.Info($"[AutoSweeperRange] Initialized with extended range: {ExtendedRange}");
        }

        public override void Start()
        {
        }

        public override void Shutdown()
        {
            ModLogger.Info("[AutoSweeperRange] Shutdown complete");
        }

        private static void AdjustSweepBotBuildingDef(ref BuildingDef __result)
        {
            if (__result != null)
            {
                __result.PermittedRotations = PermittedRotations.R360;
                ModLogger.Debug($"[AutoSweeperRange] Adjusted building def for 360 rotation");
            }
        }

        private static void AdjustPickupRadius(ref float __result)
        {
            __result = ExtendedRange;
            ModLogger.Debug($"[AutoSweeperRange] Adjusted pickup radius to: {ExtendedRange}");
        }

        private static void AdjustOperationalRadius(ref float __result)
        {
            __result = ExtendedRange;
            ModLogger.Debug($"[AutoSweeperRange] Adjusted operational radius to: {ExtendedRange}");
        }

        private static void PreDetectPickupable(object __instance, ref float radius)
        {
            radius = ExtendedRange;
            ModLogger.Debug($"[AutoSweeperRange] Adjusted detection radius to: {ExtendedRange}");
        }
    }
}
