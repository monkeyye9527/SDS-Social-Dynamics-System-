﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using HarmonyLib;
using KMod;
using System.Collections.Generic;
using UnityEngine;
using ONIModPack.Core;
using ONIModPack.Core.Modules;

namespace ONIModPack.QoL
{
    public class OverlayImprovementsModule : ModModuleBase
    {
        public OverlayImprovementsModule()
        {
            ModuleId = "QoL.UI.OverlayImprovements";
        }
        
        public override string DisplayName => "Overlay Improvements";
        public override string Description => "Improves temperature/power overlay color mapping and display format";
        public override ModuleCategory Category => ModuleCategory.QoL;
        public override string[] Dependencies => new[] { "Core" };
        public override bool DefaultEnabled => true;
        public override int Priority => ModulePriorities.Feature;

        private static readonly Color[] TemperatureGradient = new Color[]
        {
            new Color(0.0f, 0.0f, 1.0f),
            new Color(0.0f, 1.0f, 1.0f),
            new Color(0.0f, 1.0f, 0.0f),
            new Color(1.0f, 1.0f, 0.0f),
            new Color(1.0f, 0.5f, 0.0f),
            new Color(1.0f, 0.0f, 0.0f)
        };
        
        private static readonly float[] TemperatureStops = new float[]
        {
            0.0f,
            0.2f,
            0.4f,
            0.6f,
            0.8f,
            1.0f
        };
        
        private static float _minTemp = 200f;
        private static float _maxTemp = 500f;

        public override CompatibilityResult CheckCompatibility(IReadOnlyList<Mod> loadedMods)
        {
            return CompatibilityResult.Ok();
        }

        public override void RegisterPatches(Harmony harmony)
        {
            ModLogger.Info("[OverlayImprovements] Registering patches with HarmonyPriority");
            
            var tempOverlayType = AccessTools.TypeByName("TemperatureOverlay");
            if (tempOverlayType != null)
            {
                var getTooltipMethod = AccessTools.DeclaredMethod(tempOverlayType, "GetTooltip");
                if (getTooltipMethod != null)
                {
                    harmony.Patch(
                        getTooltipMethod,
                        postfix: new HarmonyMethod(typeof(OverlayImprovementsModule), nameof(OnGetTemperatureTooltip))
                    );
                    ModLogger.Info("[OverlayImprovements] Patched TemperatureOverlay.GetTooltip");
                }
                
                var getColorMethod = AccessTools.DeclaredMethod(tempOverlayType, "GetColor");
                if (getColorMethod != null)
                {
                    harmony.Patch(
                        getColorMethod,
                        postfix: new HarmonyMethod(typeof(OverlayImprovementsModule), nameof(OnGetTemperatureColor))
                    );
                    ModLogger.Info("[OverlayImprovements] Patched TemperatureOverlay.GetColor");
                }
            }
            
            var powerOverlayType = AccessTools.TypeByName("PowerOverlay");
            if (powerOverlayType != null)
            {
                var getTooltipMethod = AccessTools.DeclaredMethod(powerOverlayType, "GetTooltip");
                if (getTooltipMethod != null)
                {
                    harmony.Patch(
                        getTooltipMethod,
                        postfix: new HarmonyMethod(typeof(OverlayImprovementsModule), nameof(OnGetPowerTooltip))
                    );
                    ModLogger.Info("[OverlayImprovements] Patched PowerOverlay.GetTooltip");
                }
            }
            
            var simDebugViewType = AccessTools.TypeByName("SimDebugView");
            if (simDebugViewType != null)
            {
                var updateMethod = AccessTools.DeclaredMethod(simDebugViewType, "Update");
                if (updateMethod != null)
                {
                    harmony.Patch(
                        updateMethod,
                        postfix: new HarmonyMethod(typeof(OverlayImprovementsModule), nameof(OnSimDebugViewUpdate))
                    );
                }
            }
        }

        public override void RegisterOptions(object panel)
        {
        }

        public override void Initialize()
        {
            ModLogger.Info("[OverlayImprovements] Initialized with enhanced color gradient");
        }

        public override void Start()
        {
        }

        public override void Shutdown()
        {
            ModLogger.Info("[OverlayImprovements] Shutdown complete");
        }

        private static void OnGetTemperatureTooltip(ref string __result, float temperature)
        {
            if (string.IsNullOrEmpty(__result)) return;
            
            try
            {
                float celsius = temperature - 273.15f;
                float fahrenheit = celsius * 1.8f + 32f;
                
                __result = $"{__result}\n({celsius:F1}C / {fahrenheit:F1}F)";
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[OverlayImprovements] Failed to format tooltip: {ex.Message}");
            }
        }

        private static void OnGetTemperatureColor(ref Color __result, float temperature)
        {
            try
            {
                float normalized = Mathf.Clamp01((temperature - _minTemp) / (_maxTemp - _minTemp));
                __result = EvaluateGradient(normalized);
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[OverlayImprovements] Failed to get temperature color: {ex.Message}");
            }
        }

        private static void OnGetPowerTooltip(ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            
            try
            {
                __result = FormatPowerTooltip(__result);
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"[OverlayImprovements] Failed to format power tooltip: {ex.Message}");
            }
        }

        private static void OnSimDebugViewUpdate(object __instance)
        {
            if (__instance == null) return;
            
            try
            {
                var overlayLegend = AccessTools.Field(__instance.GetType(), "overlayLegend");
                if (overlayLegend != null)
                {
                    var legend = overlayLegend.GetValue(__instance);
                    if (legend != null)
                    {
                        UpdateLegendColors(legend);
                    }
                }
            }
            catch
            {
            }
        }

        private static Color EvaluateGradient(float t)
        {
            if (TemperatureStops.Length < 2 || TemperatureGradient.Length < 2)
                return Color.white;
            
            for (int i = 0; i < TemperatureStops.Length - 1; i++)
            {
                if (t >= TemperatureStops[i] && t <= TemperatureStops[i + 1])
                {
                    float localT = (t - TemperatureStops[i]) / (TemperatureStops[i + 1] - TemperatureStops[i]);
                    return Color.Lerp(TemperatureGradient[i], TemperatureGradient[i + 1], localT);
                }
            }
            
            return t <= 0 ? TemperatureGradient[0] : TemperatureGradient[TemperatureGradient.Length - 1];
        }

        private static void UpdateLegendColors(object legend)
        {
            try
            {
                var updateColorsMethod = HarmonyLib.AccessTools.DeclaredMethod(legend.GetType(), "UpdateColors");
                if (updateColorsMethod != null)
                {
                    updateColorsMethod.Invoke(legend, new object[] { TemperatureGradient, TemperatureStops });
                }
            }
            catch
            {
            }
        }

        private static string FormatPowerTooltip(string tooltip)
        {
            tooltip = tooltip.Replace("kW", " kW");
            tooltip = tooltip.Replace("W", " W");
            
            if (tooltip.Contains("/"))
            {
                string[] parts = tooltip.Split('/');
                if (parts.Length == 2)
                {
                    float used = ParsePowerValue(parts[0].Trim());
                    float total = ParsePowerValue(parts[1].Trim());
                    float percentage = total > 0 ? (used / total) * 100 : 0;
                    return $"{parts[0].Trim()} / {parts[1].Trim()} ({percentage:F1}%)";
                }
            }
            
            return tooltip;
        }

        private static float ParsePowerValue(string value)
        {
            value = value.Replace("kW", "").Replace("W", "").Trim();
            if (float.TryParse(value, out float result))
            {
                return result;
            }
            return 0f;
        }
        
        public static void SetTemperatureRange(float min, float max)
        {
            _minTemp = min;
            _maxTemp = max;
            ModLogger.Info($"[OverlayImprovements] Temperature range updated: {_minTemp}K - {_maxTemp}K");
        }
    }
}
