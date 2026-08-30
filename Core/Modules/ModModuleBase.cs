using HarmonyLib;
using KMod;
using System.Collections.Generic;
using ONIModPack.Core;

namespace ONIModPack.Core.Modules
{
    public abstract class ModModuleBase : ModuleBase, IModModule
    {
        public abstract string DisplayName { get; }
        
        public abstract string Description { get; }
        
        public abstract ModuleCategory Category { get; }
        
        public abstract string[] Dependencies { get; }
        
        public abstract bool DefaultEnabled { get; }
        
        public abstract int Priority { get; }
        
        public virtual CompatibilityResult CheckCompatibility(IReadOnlyList<KMod.Mod> loadedMods)
        {
            return CompatibilityResult.Ok();
        }
        
        public virtual void RegisterPatches(HarmonyLib.Harmony harmony)
        {
        }
        
        public virtual void RegisterOptions(object panel)
        {
        }
    }
}
