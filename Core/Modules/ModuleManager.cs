using HarmonyLib;
using Harmony = HarmonyLib.Harmony;
using KMod;
using ONIModPack.Core.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core
{
    public class ModuleManager
    {
        private readonly HarmonyLib.Harmony _harmony;
        private readonly Dictionary<string, IModModule> _modules = new Dictionary<string, IModModule>();
        private readonly HashSet<string> _enabledModules = new HashSet<string>();
        private readonly List<string> _loadOrder = new List<string>();
        private readonly Dictionary<string, HashSet<string>> _extraDependencies = new Dictionary<string, HashSet<string>>();
        private readonly HashSet<string> _failedModules = new HashSet<string>();
        
        private Dictionary<string, List<string>> _dependencyGraph;
        private Dictionary<string, int> _inDegreeCache;
        private bool _isFinalized = false;
        private bool _isDirty = true;

        public IReadOnlyCollection<IModModule> AllModules => _modules.Values;

        public IReadOnlyList<IModModule> LoadedModules =>
            _loadOrder.Where(id => _modules.ContainsKey(id))
                     .Select(id => _modules[id])
                     .Where(m => _enabledModules.Contains(m.ModuleId))
                     .ToList();

        public bool IsFinalized => _isFinalized;

        public ModuleManager(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
            _dependencyGraph = new Dictionary<string, List<string>>();
            _inDegreeCache = new Dictionary<string, int>();
        }

        public void RegisterModule(IModModule module)
        {
            if (_modules.ContainsKey(module.ModuleId))
            {
                ModLogger.Warning($"Module {module.ModuleId} is already registered, skipping");
                return;
            }

            _modules[module.ModuleId] = module;
            _dependencyGraph[module.ModuleId] = new List<string>();
            _inDegreeCache[module.ModuleId] = 0;

            if (module is ModModuleBase baseModule)
            {
                baseModule.SetState(ModuleState.Registered);
            }

            if (module.Category == ModuleCategory.Core || module.DefaultEnabled)
            {
                _enabledModules.Add(module.ModuleId);
            }

            MarkDirty();
            
            ModLogger.Debug($"Registered module: {module.ModuleId} [{(IsEnabled(module.ModuleId) ? "ENABLED" : "DISABLED")}]");
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public bool FinalizeRegistration()
        {
            if (_isFinalized && !_isDirty)
            {
                ModLogger.Debug("ModuleManager already finalized, skipping");
                return true;
            }

            ModLogger.Info("Finalizing module registration...");
            
            ResolveDependencies();
            
            if (!ValidateAllDependencies())
            {
                ModLogger.Error("Dependency validation failed during finalization");
                return false;
            }

            if (!RebuildLoadOrder())
            {
                ModLogger.Error("Failed to build load order (circular dependency?)");
                return false;
            }

            _isFinalized = true;
            _isDirty = false;
            
            ModLogger.Info($"Finalization complete. Load order: {string.Join(" -> ", _loadOrder)}");
            return true;
        }

        private void ResolveDependencies()
        {
            _dependencyGraph.Clear();
            _inDegreeCache.Clear();

            foreach (var moduleId in _modules.Keys)
            {
                _dependencyGraph[moduleId] = new List<string>();
                _inDegreeCache[moduleId] = 0;
            }

            foreach (var kvp in _modules)
            {
                var moduleId = kvp.Key;
                var module = kvp.Value;

                foreach (var depId in module.Dependencies)
                {
                    AddDependencyEdge(depId, moduleId);
                }

                if (_extraDependencies.TryGetValue(moduleId, out var extraDeps))
                {
                    foreach (var depId in extraDeps)
                    {
                        AddDependencyEdge(depId, moduleId);
                    }
                }
            }
        }

        private void AddDependencyEdge(string depId, string moduleId)
        {
            if (_modules.ContainsKey(depId))
            {
                if (!_dependencyGraph[depId].Contains(moduleId))
                {
                    _dependencyGraph[depId].Add(moduleId);
                    _inDegreeCache[moduleId]++;
                }
            }
            else
            {
                ModLogger.Warning($"Module {moduleId} has unresolved dependency: {depId}");
            }
        }

        private bool ValidateAllDependencies()
        {
            foreach (var kvp in _modules)
            {
                var moduleId = kvp.Key;
                var module = kvp.Value;

                foreach (var depId in module.Dependencies)
                {
                    if (!_modules.ContainsKey(depId))
                    {
                        ModLogger.Error($"Module {moduleId} depends on missing module: {depId}");
                        return false;
                    }
                }
            }
            return true;
        }

        private bool RebuildLoadOrder()
        {
            var result = KahnTopologicalSortWithPriority();
            if (result == null)
            {
                return false;
            }

            _loadOrder.Clear();
            _loadOrder.AddRange(result);

            return true;
        }

        private List<string> KahnTopologicalSortWithPriority()
        {
            var result = new List<string>();
            var inDegreeCopy = new Dictionary<string, int>(_inDegreeCache);

            var frontier = new SortedDictionary<int, Queue<string>>();

            foreach (var kvp in inDegreeCopy)
            {
                if (kvp.Value == 0)
                {
                    var priority = _modules.TryGetValue(kvp.Key, out var mod) ? mod.Priority : int.MaxValue;
                    if (!frontier.ContainsKey(priority))
                        frontier[priority] = new Queue<string>();
                    frontier[priority].Enqueue(kvp.Key);
                }
            }

            while (frontier.Count > 0)
            {
                var lowestPriority = frontier.Keys.Min();
                var queue = frontier[lowestPriority];

                if (queue.Count == 0)
                {
                    frontier.Remove(lowestPriority);
                    continue;
                }

                var current = queue.Dequeue();
                if (queue.Count == 0)
                    frontier.Remove(lowestPriority);

                result.Add(current);

                if (_dependencyGraph.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        inDegreeCopy[neighbor]--;
                        if (inDegreeCopy[neighbor] == 0)
                        {
                            var priority = _modules.TryGetValue(neighbor, out var mod) ? mod.Priority : int.MaxValue;
                            if (!frontier.ContainsKey(priority))
                                frontier[priority] = new Queue<string>();
                            frontier[priority].Enqueue(neighbor);
                        }
                    }
                }
            }

            if (result.Count != _modules.Count)
            {
                var unresolved = _modules.Keys.Where(k => !result.Contains(k)).ToList();
                ModLogger.Error($"Circular dependency detected involving modules: {string.Join(", ", unresolved)}");
                return null;
            }

            return result;
        }

        public bool EnableModule(string moduleId)
        {
            if (!_modules.ContainsKey(moduleId))
            {
                ModLogger.Warning($"Cannot enable unknown module: {moduleId}");
                return false;
            }

            var module = _modules[moduleId];

            foreach (var dep in module.Dependencies)
            {
                if (!_enabledModules.Contains(dep))
                {
                    ModLogger.Error($"Cannot enable {moduleId}: dependency {dep} is not enabled");
                    return false;
                }
            }

            _enabledModules.Add(moduleId);
            ModLogger.Info($"Module enabled: {moduleId}");
            
            OnModuleStateChanged(moduleId, true);
            
            return true;
        }

        public bool DisableModule(string moduleId)
        {
            if (_modules.TryGetValue(moduleId, out var module) && module.Category == ModuleCategory.Core)
            {
                ModLogger.Warning($"Cannot disable core module: {moduleId}");
                return false;
            }

            var dependents = _modules.Values
                .Where(m => _enabledModules.Contains(m.ModuleId))
                .Where(m => m.Dependencies.Contains(moduleId))
                .Select(m => m.ModuleId);

            if (dependents.Any())
            {
                ModLogger.Error($"Cannot disable {moduleId}: still required by {string.Join(", ", dependents)}");
                return false;
            }

            _enabledModules.Remove(moduleId);
            ModLogger.Info($"Module disabled: {moduleId}");
            
            OnModuleStateChanged(moduleId, false);
            
            return true;
        }

        private void OnModuleStateChanged(string moduleId, bool enabled)
        {
            EventBus.Publish(new ModuleStateChangedEvent
            {
                ModuleId = moduleId,
                IsEnabled = enabled
            });
        }

        public bool IsEnabled(string moduleId) => _enabledModules.Contains(moduleId);

        public IReadOnlyCollection<string> GetFailedModules() => _failedModules;

        public bool IsModuleHealthy(string moduleId) => !_failedModules.Contains(moduleId);

        public ModuleState GetModuleState(string moduleId)
        {
            if (_modules.TryGetValue(moduleId, out var module))
            {
                return module.CurrentState;
            }
            return ModuleState.Disposed;
        }

        public List<CompatibilityResult> CheckCompatibility(IReadOnlyList<Mod> loadedMods)
        {
            var results = new List<CompatibilityResult>();

            foreach (var module in LoadedModules)
            {
                try
                {
                    var result = module.CheckCompatibility(loadedMods);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(CompatibilityResult.Error(
                        $"Compatibility check failed for {module.ModuleId}: {ex.Message}"));
                }
            }
            
            results.AddRange(CheckHarmonyConflicts());

            return results;
        }
        
        private List<CompatibilityResult> CheckHarmonyConflicts()
        {
            var results = new List<CompatibilityResult>();
            
            try
            {
                var patchedMethods = _harmony.GetPatchedMethods().ToList();
                
                var methodGroups = patchedMethods
                    .GroupBy(m => $"{m.DeclaringType?.FullName}.{m.Name}")
                    .Where(g => g.Count() > 1)
                    .ToList();
                
                foreach (var group in methodGroups)
                {
                    var patchers = group.SelectMany(m => 
                    {
                        try
                        {
                            var patchInfo = HarmonyLib.Harmony.GetPatchInfo(m);
                            if (patchInfo?.Owners != null)
                            {
                                return patchInfo.Owners;
                            }
                            return Enumerable.Empty<string>();
                        }
                        catch
                        {
                            return Enumerable.Empty<string>();
                        }
                    })
                    .Where(owner => !string.IsNullOrEmpty(owner))
                    .Distinct()
                    .ToList();
                    
                    if (patchers.Count > 1)
                    {
                        results.Add(CompatibilityResult.Warning(
                            $"Potential Harmony conflict on {group.Key}: multiple patchers ({string.Join(", ", patchers)})"));
                    }
                }
                
                results.AddRange(DetectKnownModConflicts(patchedMethods));
                
                if (results.Count > 0)
                {
                    ModLogger.Warning($"[ModuleManager] Found {results.Count} potential Harmony conflicts");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[ModuleManager] Failed to check Harmony conflicts: {ex.Message}");
            }
            
            return results;
        }
        
        private List<CompatibilityResult> DetectKnownModConflicts(List<System.Reflection.MethodBase> patchedMethods)
        {
            var results = new List<CompatibilityResult>();
            
            var knownConflictMods = new[]
            {
                "AutoSweeper", "Sweeper", "RobotSweeper",
                "Robonaut", "SpaceScanner",
                "UtilitySmart", "SmartSweep"
            };
            
            var criticalMethodPatterns = new[]
            {
                "SweepCore", "FindNextSweepable",
                "GetNextDestination", "ShouldSweep"
            };
            
            foreach (var method in patchedMethods)
            {
                var methodName = method.Name;
                var declaringType = method.DeclaringType?.FullName ?? "";
                
                foreach (var pattern in criticalMethodPatterns)
                {
                    if (methodName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            var patchInfo = HarmonyLib.Harmony.GetPatchInfo(method);
                            if (patchInfo?.Owners != null)
                            {
                                var owners = patchInfo.Owners.Where(o => 
                                    knownConflictMods.Any(m => o.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                                
                                if (owners.Count > 0)
                                {
                                    results.Add(CompatibilityResult.Warning(
                                        $"[ConflictDetector] Method {declaringType}.{methodName} is patched by AutoSweeper-related mod(s): {string.Join(", ", owners)}. " +
                                        "This may cause duplicate sweeping behavior or priority conflicts."));
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                
                if (methodName.IndexOf("Priority", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        var patchInfo = HarmonyLib.Harmony.GetPatchInfo(method);
                        if (patchInfo != null)
                        {
                            var allPatches = patchInfo.Prefixes.Concat(patchInfo.Postfixes).ToList();
                            if (allPatches.Count > 1)
                            {
                                var differentPriorities = allPatches.Select(p => p.priority).Distinct().ToList();
                                if (differentPriorities.Count > 1)
                                {
                                    results.Add(CompatibilityResult.Warning(
                                        $"[ConflictDetector] Method {declaringType}.{methodName} has patches with different priorities. " +
                                        "Execution order may differ from expected."));
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            
            return results;
        }

        public void ApplyPatches()
        {
            if (!_isFinalized)
            {
                ModLogger.Warning("Cannot apply patches before FinalizeRegistration()");
                return;
            }

            foreach (var moduleId in _loadOrder)
            {
                if (!_enabledModules.Contains(moduleId)) continue;
                if (_failedModules.Contains(moduleId)) continue;

                var module = _modules[moduleId];
                try
                {
                    ModLogger.Debug($"Applying patches for {moduleId}...");
                    module.RegisterPatches(_harmony);
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[ModuleManager] Patch application failed for {moduleId}. Removing from enabled set to prevent cascade failure. Error: {ex.Message}");
                    _failedModules.Add(moduleId);
                    _enabledModules.Remove(moduleId);
                    if (module is ModModuleBase baseModule)
                    {
                        baseModule.MarkAsFailed();
                    }
                }
            }
        }

        public void InitializeLoadedModules()
        {
            if (!_isFinalized)
            {
                ModLogger.Warning("Cannot initialize modules before FinalizeRegistration()");
                return;
            }

            foreach (var module in LoadedModules)
            {
                if (_failedModules.Contains(module.ModuleId))
                {
                    ModLogger.Debug($"Skipping previously failed module: {module.ModuleId}");
                    continue;
                }

                try
                {
                    ModLogger.Debug($"Initializing {module.ModuleId}...");
                    module.Initialize();
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[ModuleManager] Module initialization failed: {module.ModuleId}. Removing from enabled set to prevent cascade failure. Error: {ex.Message}");
                    _failedModules.Add(module.ModuleId);
                    _enabledModules.Remove(module.ModuleId);
                    if (module is ModModuleBase baseModule)
                    {
                        baseModule.MarkAsFailed();
                    }
                }
            }
        }

        public void StartLoadedModules()
        {
            if (!_isFinalized)
            {
                ModLogger.Warning("Cannot start modules before FinalizeRegistration()");
                return;
            }

            foreach (var module in LoadedModules)
            {
                if (_failedModules.Contains(module.ModuleId))
                {
                    ModLogger.Debug($"Skipping previously failed module: {module.ModuleId}");
                    continue;
                }

                try
                {
                    ModLogger.Debug($"Starting {module.ModuleId}...");
                    module.Start();
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[ModuleManager] Module start failed: {module.ModuleId}. Error: {ex.Message}");
                    _failedModules.Add(module.ModuleId);
                    if (module is ModModuleBase baseModule)
                    {
                        baseModule.MarkAsFailed();
                    }
                }
            }
        }

        public void ShutdownAll()
        {
            foreach (var module in _modules.Values.Reverse())
            {
                try
                {
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"Error during shutdown of {module.ModuleId}: {ex}");
                }
            }
            
            _modules.Clear();
            _enabledModules.Clear();
            _loadOrder.Clear();
            _extraDependencies.Clear();
            _failedModules.Clear();
            _isFinalized = false;
            _isDirty = true;
            
            EventBus.Clear();
            ModLogger.Debug("[ModuleManager] EventBus cleared during shutdown");
        }
        
        public void AddModuleDependency(string moduleId, string dependencyId)
        {
            if (!_modules.ContainsKey(moduleId) || !_modules.ContainsKey(dependencyId))
            {
                ModLogger.Warning($"Cannot add dependency: module not found");
                return;
            }
            
            if (_isFinalized)
            {
                ModLogger.Warning("Cannot add dependencies after finalization");
                return;
            }

            if (!_extraDependencies.TryGetValue(moduleId, out var extraDeps))
            {
                extraDeps = new HashSet<string>();
                _extraDependencies[moduleId] = extraDeps;
            }

            var module = _modules[moduleId];
            if (!module.Dependencies.Contains(dependencyId) && extraDeps.Add(dependencyId))
            {
                MarkDirty();
                ModLogger.Debug($"Added runtime dependency: {moduleId} -> {dependencyId}");
            }
        }
    }
}