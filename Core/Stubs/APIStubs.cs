﻿﻿﻿// =====================================================================================================
// DEPRECATED: This file is obsolete and kept for backward compatibility only.
// =====================================================================================================
// 
// IMPORTANT: Game types (Grid, MinionIdentity, Components, Db, Tag, Room, etc.) should NOT be defined here!
//            They will shadow real game types in global namespace for ONIModPack.Content.* code.
//            Content layer code should use real game types directly from global namespace.
//
// Compat layer contains only:
//   - EXTENSIONS: GameHashesExtensions, ModSimAndRenderScheduler, GridRadialUtil
//   - ADAPTERS: GameObjectExtensions, TransformExtensions
//   - WRAPPERS: BuildingDefWrapper (obsolete), KAnimFileWrapper (obsolete)
//
// Real game types available from global namespace:
//   - Grid, ObjectLayer, MinionIdentity, Components, Room, RoomProber, Db, Tag, Tech, Techs
//   - LocString, EffectorValues, GameClock, Game, Scheduler, StressMonitor, IBuildingConfig
//
// This file will be removed in a future version.
// =====================================================================================================

#pragma warning disable CS0618 // Type or member is obsolete

using System;
using System.Collections.Generic;
using UnityEngine;

// Aliases to new Compat namespace types
namespace ONIModPack.Core
{
    using ONIModPack.Core.Compat;

    /// <summary>
    /// DEPRECATED: Use ONIModPack.Core.Compat.GameHashesExtensions instead.
    /// </summary>
    [Obsolete("Use ONIModPack.Core.Compat.GameHashesExtensions")]
    public static class GameHashes
    {
        public static readonly int EverySecond = GameHashesExtensions.EverySecond;
        public static readonly int EveryUpdate = GameHashesExtensions.EveryUpdate;
        public static readonly int OnSim1000ms = GameHashesExtensions.OnSim1000ms;
        public static readonly int OnSim200ms = GameHashesExtensions.OnSim200ms;
        public static readonly int OnSim100ms = GameHashesExtensions.OnSim100ms;
        public static readonly int CycleStart = GameHashesExtensions.CycleStart;
        public static readonly int MinionDied = GameHashesExtensions.MinionDied;
        public static readonly int StressChanged = GameHashesExtensions.StressChanged;
        public static readonly int BuildingComplete = GameHashesExtensions.BuildingComplete;
    }
}

// Global namespace types pointing to Compat
namespace ONIModPack
{
    using ONIModPack.Core.Compat;

    /// <summary>
    /// DEPRECATED: Use ONIModPack.Core.Compat.GameHashesExtensions instead.
    /// </summary>
    [Obsolete("Use ONIModPack.Core.Compat.GameHashesExtensions")]
    public static class GameHashesExtension
    {
        public static readonly int EverySecond = GameHashesExtensions.EverySecond;
        public static readonly int EveryUpdate = GameHashesExtensions.EveryUpdate;
    }

    /// <summary>
    /// DEPRECATED: Use ONIModPack.Core.Compat.ModSimAndRenderScheduler instead.
    /// </summary>
    [Obsolete("Use ONIModPack.Core.Compat.ModSimAndRenderScheduler")]
    public static class ModSimAndRenderScheduler
    {
        [Obsolete("Use ONIModPack.Core.Compat.ModSimAndRenderScheduler.RegisterRepeating")]
        public static void RegisterRepeating(float intervalSeconds, System.Action action)
            => Core.Compat.ModSimAndRenderScheduler.RegisterRepeating(intervalSeconds, action);

        [Obsolete("Use ONIModPack.Core.Compat.ModSimAndRenderScheduler.Unregister")]
        public static void Unregister(System.Action action)
            => Core.Compat.ModSimAndRenderScheduler.Unregister(action);

        [Obsolete("Use ONIModPack.Core.Compat.ModSimAndRenderScheduler.Update")]
        public static void Update()
            => Core.Compat.ModSimAndRenderScheduler.Update();
    }
}

// WARNING: DO NOT ADD ANY GAME TYPE STUBS IN ONIModPack.Content NAMESPACE!
// 
// REASON: C# type resolution will find these "closer" types first,
//         shadowing the real game types in the global namespace.
//         This causes ALL Content layer code to use fake/stub types
//         instead of the real ONI game types (Grid, MinionIdentity, etc.)
//
// SOLUTION: Use ONIModPack.Core.Compat for extensions/adapters only.
//           Let real game types resolve from global namespace automatically.
//           If you need to wrap/extend game types, do it in Compat layer.
//
namespace ONIModPack.Content
{
    // EMPTY - intentionally left blank to prevent type shadowing
    // Any types here will shadow game types for all ONIModPack.Content.* namespaces
}

#pragma warning restore CS0618
