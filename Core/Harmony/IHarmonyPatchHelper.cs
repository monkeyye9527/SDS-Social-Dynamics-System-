using System;
using System.Collections.Generic;
using System.Reflection;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Harmony
{
    public interface IHarmonyPatchHelper : IService
    {
        string HarmonyId { get; }
        
        void RegisterPatch(PatchInfo patch);
        void RegisterPatch<T>(string methodName, MethodInfo prefix = null, MethodInfo postfix = null,
                              MethodInfo transpiler = null, MethodInfo finalizer = null,
                              PatchPriority priority = PatchPriority.Normal);
        void ApplyAllPatches();
        void UnpatchAll();
        bool IsPatchApplied(string patchId);
        List<PatchInfo> GetAllPatches();
    }
}
