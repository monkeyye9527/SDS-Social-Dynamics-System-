﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using HarmonyLib;
using KMod;
using System.Collections.Generic;
using ONIModPack.Core;
using ONIModPack.Core.Modules;

namespace ONIModPack.Balance
{
    public class HungerModule : ModModuleBase
    {
        public HungerModule()
        {
            ModuleId = "Balance.Difficulty.Hunger";
        }
        
        public override string DisplayName => "Hunger Difficulty Adjustment";
        public override string Description => "Adjusts duplicant hunger consumption rate";
        public override ModuleCategory Category => ModuleCategory.Balance;
        public override string[] Dependencies => new[] { "Core" };
        public override bool DefaultEnabled => false;
        public override int Priority => ModulePriorities.Feature;

        public override CompatibilityResult CheckCompatibility(IReadOnlyList<Mod> loadedMods)
        {
            return CompatibilityResult.Ok();
        }

        public override void RegisterPatches(Harmony harmony)
        {
            ONIModPack.Core.Logger.Debug("[HungerModule] Registering patches");
        }

        public override void RegisterOptions(object panel)
        {
        }

        public override void Initialize()
        {
            ONIModPack.Core.Logger.Info("[HungerModule] Initializing hunger balance module");
        }

        public override void Start()
        {
        }

        public override void Shutdown()
        {
            ONIModPack.Core.Logger.Debug("[HungerModule] Shutting down");
        }
    }
}
