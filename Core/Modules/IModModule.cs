using HarmonyLib;
using KMod;
using System.Collections.Generic;
using ONIModPack.Core.Modules;

namespace ONIModPack.Core
{
    public interface IModModule : IModuleState
    {
        string ModuleId { get; }
        string DisplayName { get; }
        string Description { get; }
        ModuleCategory Category { get; }
        string[] Dependencies { get; }
        bool DefaultEnabled { get; }
        int Priority { get; }
        
        CompatibilityResult CheckCompatibility(IReadOnlyList<KMod.Mod> loadedMods);
        void RegisterPatches(HarmonyLib.Harmony harmony);
        void RegisterOptions(object panel);
        void Initialize();
        void Start();
        void Shutdown();
    }

    public enum ModuleCategory
    {
        Core,
        QoL,
        Balance,
        Content
    }
    
    public static class ModulePriorities
    {
        public const int System = 0;
        public const int Framework = 100;
        public const int Content = 200;
        public const int Feature = 300;
        public const int UI = 400;
        public const int Debug = 500;
    }

    public class CompatibilityResult
    {
        public CompatibilitySeverity Severity { get; set; }
        public string Message { get; set; }
        public string RelatedModId { get; set; }

        public static CompatibilityResult Ok() => 
            new() { Severity = CompatibilitySeverity.Info, Message = "Compatible" };
            
        public static CompatibilityResult Warning(string message, string modId = null) => 
            new() { Severity = CompatibilitySeverity.Warning, Message = message, RelatedModId = modId };
            
        public static CompatibilityResult Error(string message, string modId = null) => 
            new() { Severity = CompatibilitySeverity.Error, Message = message, RelatedModId = modId };
    }

    public enum CompatibilitySeverity
    {
        Info,
        Warning,
        Error
    }
}