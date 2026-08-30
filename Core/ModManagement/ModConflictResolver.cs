using System;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core.ModManagement
{
    public enum ConflictResolutionType
    {
        WarnOnly,
        DisableModule,
        OverridePriority,
        MergeConfig,
        DisableOtherMod
    }

    public enum ConflictSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class ModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> Conflicts { get; set; } = new List<string>();
        public int Priority { get; set; } = 0;
        public bool IsEnabled { get; set; } = true;
        public bool IsBuiltIn { get; set; } = false;
    }

    public class ConflictInfo
    {
        public string ModuleId { get; set; }
        public string ConflictingModId { get; set; }
        public string Description { get; set; }
        public ConflictSeverity Severity { get; set; }
        public ConflictResolutionType Resolution { get; set; }
        public bool IsResolved { get; set; }
    }

    public class ModConflictResolver : IModConflictResolver
    {
        private readonly List<ModInfo> _registeredMods = new List<ModInfo>();
        private readonly List<ConflictInfo> _conflicts = new List<ConflictInfo>();
        private bool _isInitialized;

        public event Action<ConflictInfo> OnConflictDetected;
        public event Action<ConflictInfo> OnConflictResolved;

        public bool IsInitialized => _isInitialized;

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            ModLogger.Info("[ModConflictResolver] Initialized");
        }

        public void Shutdown()
        {
            _registeredMods.Clear();
            _conflicts.Clear();
            OnConflictDetected = null;
            OnConflictResolved = null;
            _isInitialized = false;
            ModLogger.Debug("[ModConflictResolver] Shutdown");
        }

        public void RegisterMod(ModInfo modInfo)
        {
            if (modInfo == null) return;

            var existing = _registeredMods.FirstOrDefault(m => m.Id == modInfo.Id);
            if (existing != null)
            {
                _registeredMods.Remove(existing);
            }

            _registeredMods.Add(modInfo);
            CheckForConflicts(modInfo);
            ModLogger.Debug($"[ModConflictResolver] Registered mod: {modInfo.Id} v{modInfo.Version}");
        }

        public void UnregisterMod(string modId)
        {
            _registeredMods.RemoveAll(m => m.Id == modId);
            _conflicts.RemoveAll(c => c.ModuleId == modId || c.ConflictingModId == modId);
            ModLogger.Debug($"[ModConflictResolver] Unregistered mod: {modId}");
        }

        public List<ConflictInfo> CheckForConflicts(ModInfo modInfo)
        {
            var newConflicts = new List<ConflictInfo>();
            foreach (var otherMod in _registeredMods.Where(m => m.Id != modInfo.Id))
            {
                var conflict = CheckModConflict(modInfo, otherMod);
                if (conflict != null)
                {
                    newConflicts.Add(conflict);
                }
            }
            return newConflicts;
        }

        private ConflictInfo CheckModConflict(ModInfo mod1, ModInfo mod2)
        {
            bool hasConflict = false;
            string conflictDescription = string.Empty;
            ConflictSeverity severity = ConflictSeverity.Low;

            if (mod1.Conflicts.Contains(mod2.Id) || mod2.Conflicts.Contains(mod1.Id))
            {
                hasConflict = true;
                conflictDescription = $"Explicit conflict declared between {mod1.Id} and {mod2.Id}";
                severity = ConflictSeverity.High;
            }

            if (mod1.Dependencies.Intersect(mod2.Dependencies).Any())
            {
                hasConflict = true;
                conflictDescription = $"Shared dependencies between {mod1.Id} and {mod2.Id}";
                severity = ConflictSeverity.Medium;
            }

            if (hasConflict)
            {
                var conflict = new ConflictInfo
                {
                    ModuleId = mod1.Id,
                    ConflictingModId = mod2.Id,
                    Description = conflictDescription,
                    Severity = severity,
                    Resolution = ConflictResolutionType.WarnOnly,
                    IsResolved = false
                };

                _conflicts.Add(conflict);
                OnConflictDetected?.Invoke(conflict);

                ModLogger.Warning($"[ModConflictResolver] Conflict detected: {conflictDescription}");
                return conflict;
            }
            return null;
        }

        public void ResolveConflict(string conflictId, ConflictResolutionType resolution)
        {
            var conflict = _conflicts.FirstOrDefault(c => c.ModuleId == conflictId || c.ConflictingModId == conflictId);
            if (conflict != null)
            {
                ApplyResolution(conflict, resolution);
            }
        }

        public void ResolveAllConflicts(ConflictResolutionType defaultResolution)
        {
            foreach (var conflict in _conflicts.Where(c => !c.IsResolved))
            {
                ApplyResolution(conflict, defaultResolution);
            }
        }

        private void ApplyResolution(ConflictInfo conflict, ConflictResolutionType resolution)
        {
            switch (resolution)
            {
                case ConflictResolutionType.WarnOnly:
                    conflict.Resolution = ConflictResolutionType.WarnOnly;
                    conflict.IsResolved = true;
                    break;

                case ConflictResolutionType.DisableModule:
                    var module = _registeredMods.FirstOrDefault(m => m.Id == conflict.ModuleId);
                    if (module != null)
                    {
                        module.IsEnabled = false;
                        conflict.Resolution = ConflictResolutionType.DisableModule;
                        conflict.IsResolved = true;
                        ModLogger.Info($"[ModConflictResolver] Disabled module {conflict.ModuleId} due to conflict");
                    }
                    break;

                case ConflictResolutionType.OverridePriority:
                    var mod = _registeredMods.FirstOrDefault(m => m.Id == conflict.ModuleId);
                    var otherMod = _registeredMods.FirstOrDefault(m => m.Id == conflict.ConflictingModId);
                    if (mod != null && otherMod != null)
                    {
                        if (mod.Priority <= otherMod.Priority)
                        {
                            mod.Priority = otherMod.Priority + 1;
                        }
                        conflict.Resolution = ConflictResolutionType.OverridePriority;
                        conflict.IsResolved = true;
                        ModLogger.Info($"[ModConflictResolver] Priority override for {conflict.ModuleId}");
                    }
                    break;

                case ConflictResolutionType.DisableOtherMod:
                    var conflictingMod = _registeredMods.FirstOrDefault(m => m.Id == conflict.ConflictingModId);
                    if (conflictingMod != null && !conflictingMod.IsBuiltIn)
                    {
                        conflictingMod.IsEnabled = false;
                        conflict.Resolution = ConflictResolutionType.DisableOtherMod;
                        conflict.IsResolved = true;
                        ModLogger.Info($"[ModConflictResolver] Disabled conflicting mod {conflict.ConflictingModId}");
                    }
                    break;
            }

            OnConflictResolved?.Invoke(conflict);
        }

        public List<ConflictInfo> GetAllConflicts()
        {
            return _conflicts.ToList();
        }

        public List<ConflictInfo> GetUnresolvedConflicts()
        {
            return _conflicts.Where(c => !c.IsResolved).ToList();
        }

        public List<ModInfo> GetAllMods()
        {
            return _registeredMods.ToList();
        }

        public List<ModInfo> GetRegisteredMods()
        {
            return _registeredMods.ToList();
        }

        public ModInfo GetModById(string modId)
        {
            return _registeredMods.FirstOrDefault(m => m.Id == modId);
        }

        public int GetConflictCount()
        {
            return _conflicts.Count;
        }

        public int GetUnresolvedConflictCount()
        {
            return _conflicts.Count(c => !c.IsResolved);
        }

        public void CheckCompatibility()
        {
            foreach (var mod in _registeredMods)
            {
                if (!mod.IsEnabled)
                {
                    ModLogger.Warning($"[ModConflictResolver] Mod {mod.Id} is disabled");
                }
            }

            if (_conflicts.Any(c => c.Severity == ConflictSeverity.Critical))
            {
                ModLogger.Error("[ModConflictResolver] Critical conflicts detected!");
            }
        }

        public void Reset()
        {
            _registeredMods.Clear();
            _conflicts.Clear();
            ModLogger.Info("[ModConflictResolver] Reset");
        }
    }
}