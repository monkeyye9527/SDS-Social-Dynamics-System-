﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.IO;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialDynamics;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Core
{
    public class BuildingEffectConfig
    {
        public string BuildingId { get; set; }
        public Dictionary<string, float> Effects { get; set; } = new Dictionary<string, float>();
        public string Description { get; set; }
    }

    public static class BuildingEffectRegistry
    {
        private static readonly Dictionary<string, BuildingEffectConfig> _effects = new Dictionary<string, BuildingEffectConfig>();
        private static readonly Dictionary<string, float> _activeEffects = new Dictionary<string, float>();
        private static bool _isInitialized;

        public static float PublicOpinionBonus => _activeEffects.TryGetValue("opinion", out var v) ? v : 0f;
        public static float StrikeModifier => _activeEffects.TryGetValue("strike", out var v) ? v : 0f;
        public static float PolarizationModifier => _activeEffects.TryGetValue("polarization", out var v) ? v : 0f;
        public static float HappinessBonus => _activeEffects.TryGetValue("happiness", out var v) ? v : 0f;
        public static float StressReduction => _activeEffects.TryGetValue("stress", out var v) ? v : 0f;
        public static float CohesionBonus => _activeEffects.TryGetValue("cohesion", out var v) ? v : 0f;
        public static float LawSpeedBonus => _activeEffects.TryGetValue("lawSpeed", out var v) ? v : 0f;
        public static float ResearchBonus => _activeEffects.TryGetValue("research", out var v) ? v : 0f;
        public static float MoraleBonus => _activeEffects.TryGetValue("morale", out var v) ? v : 0f;

        public static void Initialize()
        {
            if (_isInitialized) return;
            
            LoadConfig();
            _isInitialized = true;
            ModLogger.Info($"[BuildingEffects] Loaded {_effects.Count} building effect configurations");
        }

        private static void LoadConfig()
        {
            var configPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "config", "building_effects.yaml");
            if (File.Exists(configPath))
            {
                try
                {
                    var yamlContent = File.ReadAllText(configPath);
                    ParseYamlConfig(yamlContent);
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[BuildingEffects] Failed to load config: {ex.Message}");
                    LoadDefaultConfig();
                }
            }
            else
            {
                LoadDefaultConfig();
            }
        }

        private static void ParseYamlConfig(string yamlContent)
        {
            string[] lines = yamlContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            BuildingEffectConfig currentConfig = null;
            
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("#")) continue;
                
                if (!trimmed.StartsWith(" "))
                {
                    string buildingId = trimmed.TrimEnd(':');
                    currentConfig = new BuildingEffectConfig { BuildingId = buildingId };
                    _effects[buildingId] = currentConfig;
                }
                else if (currentConfig != null && trimmed.IndexOf(':') >= 0)
                {
                    int colonIndex = trimmed.IndexOf(':');
                    string key = trimmed.Substring(0, colonIndex).Trim();
                    string value = trimmed.Substring(colonIndex + 1).Trim();
                    
                    if (float.TryParse(value, out float floatValue))
                    {
                        currentConfig.Effects[key] = floatValue;
                    }
                    else if (key.Equals("description", StringComparison.OrdinalIgnoreCase))
                    {
                        currentConfig.Description = value;
                    }
                }
            }
        }

        private static void LoadDefaultConfig()
        {
            _effects["BroadcastStation"] = new BuildingEffectConfig
            {
                BuildingId = "BroadcastStation",
                Effects = { { "opinion", 0.15f }, { "happiness", 0.05f } },
                Description = "Improves public opinion and happiness"
            };

            _effects["WelfareCenter"] = new BuildingEffectConfig
            {
                BuildingId = "WelfareCenter",
                Effects = { { "strike", -0.30f }, { "stress", -0.10f } },
                Description = "Reduces strike probability and stress"
            };

            _effects["CommunityCenter"] = new BuildingEffectConfig
            {
                BuildingId = "CommunityCenter",
                Effects = { { "polarization", -0.15f }, { "cohesion", 0.10f } },
                Description = "Reduces faction polarization, increases community cohesion"
            };

            _effects["ThinkTank"] = new BuildingEffectConfig
            {
                BuildingId = "ThinkTank",
                Effects = { { "lawSpeed", 0.20f }, { "research", 0.10f } },
                Description = "Speeds up law passage and research"
            };

            _effects["SolarPanelT2"] = new BuildingEffectConfig
            {
                BuildingId = "SolarPanelT2",
                Effects = { { "power", 0.15f } },
                Description = "More efficient solar power"
            };

            _effects["CommunityGarden"] = new BuildingEffectConfig
            {
                BuildingId = "CommunityGarden",
                Effects = { { "food", 0.10f }, { "morale", 0.05f } },
                Description = "Provides food and boosts morale"
            };
        }

        public static void ApplyEffect(string buildingId)
        {
            if (!_effects.TryGetValue(buildingId, out var config))
            {
                ModLogger.Debug($"[BuildingEffects] No effects defined for {buildingId}");
                return;
            }

            foreach (var effect in config.Effects)
            {
                AddActiveEffect(effect.Key, effect.Value);
            }

            ModLogger.Info($"[BuildingEffects] Applied effects for {buildingId}: {string.Join(", ", config.Effects.Keys)}");
        }

        private static void AddActiveEffect(string effectType, float value)
        {
            if (!_activeEffects.ContainsKey(effectType))
                _activeEffects[effectType] = 0f;
            
            _activeEffects[effectType] += value;
            ApplyEffectToSystems(effectType, _activeEffects[effectType]);
        }

        private static void ApplyEffectToSystems(string effectType, float totalValue)
        {
            switch (effectType.ToLower())
            {
                case "opinion":
                    var factionSystem = ServiceResolver.OptionalService<FactionSystem>();
                    if (factionSystem != null)
                        factionSystem.PublicOpinionModifier = totalValue;
                    break;
                case "strike":
                    var strikeSystem = ServiceResolver.OptionalService<IStrikeSystem>();
                    if (strikeSystem != null)
                        strikeSystem.StrikeProbabilityModifier = totalValue;
                    break;
                case "polarization":
                    var factionSys = ServiceResolver.OptionalService<FactionSystem>();
                    if (factionSys != null)
                        factionSys.PolarizationModifier = totalValue;
                    break;
                case "lawSpeed":
                case "happiness":
                case "stress":
                case "cohesion":
                case "research":
                case "power":
                case "food":
                case "morale":
                    break;
                default:
                    ModLogger.Warning($"[BuildingEffects] Unknown effect type: {effectType}");
                    break;
            }
        }

        public static float GetEffectValue(string buildingId, string effectType, float defaultValue = 0f)
        {
            if (_effects.TryGetValue(buildingId, out var config) && config.Effects.TryGetValue(effectType, out float value))
            {
                return value;
            }
            return defaultValue;
        }

        public static bool HasEffects(string buildingId)
        {
            return _effects.ContainsKey(buildingId) && _effects[buildingId].Effects.Count > 0;
        }

        public static BuildingEffectConfig GetConfig(string buildingId)
        {
            return _effects.TryGetValue(buildingId, out var config) ? config : null;
        }

        public static void Reload()
        {
            _effects.Clear();
            LoadConfig();
            ModLogger.Info("[BuildingEffects] Config reloaded");
        }
    }
}

