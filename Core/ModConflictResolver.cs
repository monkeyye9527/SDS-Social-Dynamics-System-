using KMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core
{
    public enum ConflictResolution
    {
        Allow,
        Warn,
        DisableModule,
        OverridePriority
    }

    public class ModConflictRule
    {
        public string ConflictingModId { get; set; }
        public string ConflictingModTitlePattern { get; set; }
        public ConflictResolution Resolution { get; set; } = ConflictResolution.Warn;
        public string Message { get; set; }
        public int OverrideHarmonyPriority { get; set; } = 400;
    }

    public static class ModConflictResolver
    {
        private static readonly Dictionary<string, List<ModConflictRule>> _rules =
            new Dictionary<string, List<ModConflictRule>>(StringComparer.OrdinalIgnoreCase);

        static ModConflictResolver()
        {
            RegisterDefaultRules();
        }

        public static void RegisterRule(string moduleId, ModConflictRule rule)
        {
            if (!_rules.TryGetValue(moduleId, out var list))
            {
                list = new List<ModConflictRule>();
                _rules[moduleId] = list;
            }
            list.Add(rule);
        }

        public static CompatibilityResult CheckModule(IModModule module, IReadOnlyList<KMod.Mod> loadedMods)
        {
            if (module == null || loadedMods == null || loadedMods.Count == 0)
                return CompatibilityResult.Ok();

            if (!_rules.TryGetValue(module.ModuleId, out var rules))
                return CompatibilityResult.Ok();

            foreach (var rule in rules)
            {
                var conflict = FindConflict(loadedMods, rule);
                if (conflict == null) continue;

                var modLabel = conflict.label.ToString();

                switch (rule.Resolution)
                {
                    case ConflictResolution.DisableModule:
                        ModLogger.Warning($"[ConflictResolver] Disabling {module.ModuleId} due to conflict with {modLabel}");
                        if (ModEntry.ModuleMgr != null)
                            ModEntry.ModuleMgr.DisableModule(module.ModuleId);
                        return CompatibilityResult.Error(
                            rule.Message ?? $"Disabled: conflicts with {modLabel}",
                            conflict.staticID);

                    case ConflictResolution.OverridePriority:
                        ModLogger.Info($"[ConflictResolver] {module.ModuleId} takes priority over {modLabel}");
                        return CompatibilityResult.Warning(
                            rule.Message ?? $"Priority override active over {modLabel}",
                            conflict.staticID);

                    case ConflictResolution.Warn:
                        return CompatibilityResult.Warning(
                            rule.Message ?? $"Potential conflict with {modLabel}",
                            conflict.staticID);

                    default:
                        return CompatibilityResult.Ok();
                }
            }

            return CompatibilityResult.Ok();
        }

        public static List<CompatibilityResult> ResolveAll(
            ModuleManager moduleManager,
            IReadOnlyList<KMod.Mod> loadedMods)
        {
            var results = new List<CompatibilityResult>();
            if (moduleManager == null) return results;

            foreach (var module in moduleManager.AllModules)
            {
                var result = CheckModule(module, loadedMods);
                if (result != null && result.Severity != CompatibilitySeverity.Info)
                    results.Add(result);
            }

            return results;
        }

        private static KMod.Mod FindConflict(IReadOnlyList<KMod.Mod> loadedMods, ModConflictRule rule)
        {
            foreach (var mod in loadedMods)
            {
                if (!string.IsNullOrEmpty(rule.ConflictingModId) &&
                    string.Equals(mod.staticID, rule.ConflictingModId, StringComparison.OrdinalIgnoreCase))
                    return mod;

                if (!string.IsNullOrEmpty(rule.ConflictingModTitlePattern))
                {
                    var title = mod.label.ToString();
                    if (title.IndexOf(rule.ConflictingModTitlePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        return mod;
                }
            }
            return null;
        }

        private static void RegisterDefaultRules()
        {
            RegisterRule("QoL.Convenience.SmartStorage", new ModConflictRule
            {
                ConflictingModTitlePattern = "Smart Storage",
                Resolution = ConflictResolution.Warn,
                Message = "Another smart storage mod detected; priority adjustments may conflict"
            });

            RegisterRule("QoL.Automation.AutoSweeperRange", new ModConflictRule
            {
                ConflictingModTitlePattern = "SweepBot",
                Resolution = ConflictResolution.DisableModule,
                Message = "Auto Sweeper Range disabled: incompatible sweep bot mod detected"
            });

            RegisterRule("Content.BeliefSystem", new ModConflictRule
            {
                ConflictingModTitlePattern = "Belief",
                Resolution = ConflictResolution.OverridePriority,
                Message = "Belief system active; external belief mod may be overridden"
            });
        }
    }
}
