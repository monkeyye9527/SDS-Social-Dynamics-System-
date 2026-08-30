using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ONIModPack.Core.Harmony
{
    public enum PatchPriority
    {
        Lowest = 0,
        Low = 25,
        Normal = 50,
        High = 75,
        Highest = 100
    }

    public enum PatchType
    {
        Prefix,
        Postfix,
        Transpiler,
        Finalizer
    }

    public class PatchInfo
    {
        public string Id { get; set; }
        public Type TargetType { get; set; }
        public string TargetMethodName { get; set; }
        public MethodInfo TargetMethod { get; set; }
        public MethodInfo Prefix { get; set; }
        public MethodInfo Postfix { get; set; }
        public MethodInfo Transpiler { get; set; }
        public MethodInfo Finalizer { get; set; }
        public PatchPriority Priority { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class HarmonyPatchHelper : IHarmonyPatchHelper
    {
        private HarmonyLib.Harmony _harmonyInstance;
        private readonly List<PatchInfo> _registeredPatches = new List<PatchInfo>();
        private bool _isInitialized;

        public string HarmonyId => "com.github.ONIModPack.SocialDynamics";
        public bool IsInitialized => _isInitialized;

        public void Initialize()
        {
            if (_isInitialized) return;

            _harmonyInstance = new HarmonyLib.Harmony(HarmonyId);
            _isInitialized = true;
            ModLogger.Info($"[HarmonyPatchHelper] Initialized with Harmony ID: {HarmonyId}");
        }

        public void Shutdown()
        {
            UnpatchAll();
            _registeredPatches.Clear();
            _isInitialized = false;
            ModLogger.Debug("[HarmonyPatchHelper] Shutdown");
        }

        public void RegisterPatch(PatchInfo patch)
        {
            if (patch == null) return;

            _registeredPatches.Add(patch);
            ModLogger.Debug($"[HarmonyPatchHelper] Registered patch: {patch.Id}");
        }

        public void RegisterPatch<T>(string methodName, MethodInfo prefix = null, MethodInfo postfix = null, 
                                     MethodInfo transpiler = null, MethodInfo finalizer = null,
                                     PatchPriority priority = PatchPriority.Normal)
        {
            var patch = new PatchInfo
            {
                Id = $"{typeof(T).Name}.{methodName}",
                TargetType = typeof(T),
                TargetMethodName = methodName,
                Prefix = prefix,
                Postfix = postfix,
                Transpiler = transpiler,
                Finalizer = finalizer,
                Priority = priority,
                Enabled = true
            };
            _registeredPatches.Add(patch);
        }

        public void ApplyAllPatches()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            foreach (var patch in _registeredPatches.Where(p => p.Enabled))
            {
                ApplyPatch(patch);
            }

            ModLogger.Info($"[HarmonyPatchHelper] Applied {_registeredPatches.Count(p => p.Enabled)} patches");
        }

        private void ApplyPatch(PatchInfo patch)
        {
            try
            {
                var targetMethod = patch.TargetMethod ?? FindMethod(patch.TargetType, patch.TargetMethodName);
                if (targetMethod == null)
                {
                    ModLogger.Warning($"[HarmonyPatchHelper] Target method not found: {patch.TargetType.Name}.{patch.TargetMethodName}");
                    return;
                }

                var harmonyMethod = new HarmonyMethod();
                harmonyMethod.priority = (int)patch.Priority;

                if (patch.Prefix != null)
                {
                    var prefixMethod = new HarmonyMethod(patch.Prefix) { priority = (int)patch.Priority };
                    _harmonyInstance.Patch(targetMethod, prefix: prefixMethod);
                }

                if (patch.Postfix != null)
                {
                    var postfixMethod = new HarmonyMethod(patch.Postfix) { priority = (int)patch.Priority };
                    _harmonyInstance.Patch(targetMethod, postfix: postfixMethod);
                }

                if (patch.Transpiler != null)
                {
                    var transpilerMethod = new HarmonyMethod(patch.Transpiler) { priority = (int)patch.Priority };
                    _harmonyInstance.Patch(targetMethod, transpiler: transpilerMethod);
                }

                if (patch.Finalizer != null)
                {
                    var finalizerMethod = new HarmonyMethod(patch.Finalizer) { priority = (int)patch.Priority };
                    _harmonyInstance.Patch(targetMethod, finalizer: finalizerMethod);
                }

                ModLogger.Debug($"[HarmonyPatchHelper] Applied patch: {patch.Id}");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[HarmonyPatchHelper] Failed to apply patch {patch.Id}: {ex.Message}");
            }
        }

        public void ApplyPatchById(string patchId)
        {
            var patch = _registeredPatches.FirstOrDefault(p => p.Id == patchId);
            if (patch != null)
            {
                ApplyPatch(patch);
            }
        }

        public void RemovePatchById(string patchId)
        {
            var patch = _registeredPatches.FirstOrDefault(p => p.Id == patchId);
            if (patch != null)
            {
                try
                {
                    var targetMethod = patch.TargetMethod ?? FindMethod(patch.TargetType, patch.TargetMethodName);
                    if (targetMethod != null)
                    {
                        _harmonyInstance.Unpatch(targetMethod, HarmonyPatchType.All, HarmonyId);
                    }
                    ModLogger.Debug($"[HarmonyPatchHelper] Removed patch: {patchId}");
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[HarmonyPatchHelper] Failed to remove patch {patchId}: {ex.Message}");
                }
            }
        }

        public void UnpatchAll()
        {
            _harmonyInstance?.UnpatchAll(HarmonyId);
            ModLogger.Info("[HarmonyPatchHelper] Removed all patches");
        }

        private MethodInfo FindMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        public List<PatchInfo> GetAllPatches()
        {
            return new List<PatchInfo>(_registeredPatches);
        }

        public bool IsPatchApplied(string patchId)
        {
            var patch = _registeredPatches.FirstOrDefault(p => p.Id == patchId);
            return patch != null && patch.Enabled;
        }

        public int GetPatchCount()
        {
            return _registeredPatches.Count;
        }

        public void SetPatchEnabled(string patchId, bool enabled)
        {
            var patch = _registeredPatches.FirstOrDefault(p => p.Id == patchId);
            if (patch != null)
            {
                patch.Enabled = enabled;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatchAttribute : Attribute
    {
        public Type TargetType { get; }
        public string MethodName { get; }
        public PatchType PatchType { get; }
        public PatchPriority Priority { get; }

        public HarmonyPatchAttribute(Type targetType, string methodName, PatchType patchType = PatchType.Prefix, 
                                     PatchPriority priority = PatchPriority.Normal)
        {
            TargetType = targetType;
            MethodName = methodName;
            PatchType = patchType;
            Priority = priority;
        }
    }
}