using HarmonyLib;
using System;
using System.Reflection;
using ONIModPack.Core.Diagnostics;

namespace ONIModPack.Core
{
    public static class HarmonyPatchHelper
    {
        public static HarmonyLib.Harmony GetHarmony(HarmonyLib.Harmony fallback = null) =>
            ModEntry.HarmonyInstance ?? fallback ?? throw new InvalidOperationException("Harmony not initialized");

        public static bool TryPatchPrefix(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            try
            {
                var target = ResolveMethod(targetType, methodName, argumentTypes);
                if (target == null) return false;

                var prefix = CreatePatchMethod(patchType, patchMethodName, HarmonyLib.HarmonyPatchType.Prefix);
                if (prefix == null) return false;

                harmony.Patch(target, prefix: prefix);
                ModLogger.Debug($"[PatchHelper] Patched Prefix: {targetType.Name}.{methodName}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[PatchHelper] Failed to patch Prefix {targetType.Name}.{methodName}: {ex.Message}");
                return false;
            }
        }

        public static bool TryPatchPostfix(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            try
            {
                var target = ResolveMethod(targetType, methodName, argumentTypes);
                if (target == null) return false;

                var postfix = CreatePatchMethod(patchType, patchMethodName, HarmonyLib.HarmonyPatchType.Postfix);
                if (postfix == null) return false;

                harmony.Patch(target, postfix: postfix);
                ModLogger.Debug($"[PatchHelper] Patched Postfix: {targetType.Name}.{methodName}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[PatchHelper] Failed to patch Postfix {targetType.Name}.{methodName}: {ex.Message}");
                return false;
            }
        }

        public static bool TryPatchTranspiler(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            try
            {
                var target = ResolveMethod(targetType, methodName, argumentTypes);
                if (target == null) return false;

                var transpiler = CreatePatchMethod(patchType, patchMethodName, HarmonyLib.HarmonyPatchType.Transpiler);
                if (transpiler == null) return false;

                harmony.Patch(target, transpiler: transpiler);
                ModLogger.Debug($"[PatchHelper] Patched Transpiler: {targetType.Name}.{methodName}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[PatchHelper] Failed to patch Transpiler {targetType.Name}.{methodName}: {ex.Message}");
                return false;
            }
        }

        public static void PatchPrefix(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            TryPatchPrefix(harmony, targetType, methodName, patchType, patchMethodName, argumentTypes);
        }

        public static void PatchPostfix(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            TryPatchPostfix(harmony, targetType, methodName, patchType, patchMethodName, argumentTypes);
        }

        public static void PatchTranspiler(
            HarmonyLib.Harmony harmony,
            Type targetType,
            string methodName,
            Type patchType,
            string patchMethodName,
            Type[] argumentTypes = null)
        {
            TryPatchTranspiler(harmony, targetType, methodName, patchType, patchMethodName, argumentTypes);
        }

        private static MethodInfo ResolveMethod(Type targetType, string methodName, Type[] argumentTypes)
        {
            if (targetType == null)
            {
                ModLogger.Warning($"[PatchHelper] Target type is null for method: {methodName}");
                return null;
            }

            var method = argumentTypes == null
                ? AccessTools.DeclaredMethod(targetType, methodName)
                : AccessTools.DeclaredMethod(targetType, methodName, argumentTypes);

            if (method == null)
            {
                method = argumentTypes == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, argumentTypes);
            }

            if (method == null)
            {
                ModLogger.Warning($"[PatchHelper] Method not found: {targetType.Name}.{methodName}");
            }

            return method;
        }

        private static HarmonyLib.HarmonyMethod CreatePatchMethod(Type patchType, string patchMethodName, HarmonyLib.HarmonyPatchType patchType_)
        {
            var method = AccessTools.Method(patchType, patchMethodName);
            if (method == null)
            {
                ModLogger.Warning($"[PatchHelper] Patch method not found: {patchType.Name}.{patchMethodName}");
                return null;
            }

            return new HarmonyMethod(method)
            {
                priority = Priority.Normal
            };
        }
    }
}
