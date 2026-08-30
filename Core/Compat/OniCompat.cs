// =====================================================================================================
// ONI Compatibility Layer
// =====================================================================================================
// This file provides compatibility shims for the Oxygen Not Included mod API.
// Types are categorized as:
//   - ADAPTERS: Wrap/extend game types (safe)
//   - EXTENSIONS: Add methods to game types (safe)
//   - STUBS: Fallback implementations for missing types (use with caution)
//
// WARNING: Do NOT define types that already exist in the game assemblies!
//          This will cause CS0433 "duplicate type" errors at runtime.
// =====================================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ONIModPack.Core.Compat
{
    // =====================================================================================================
    // PART 1: EXTENSIONS - Add methods to existing game types (SAFE)
    // =====================================================================================================

    /// <summary>
    /// EXTENSION: Game event hashes. These values may already exist in the game.
    /// Only define if not found in game assemblies.
    /// </summary>
    public static class GameHashesExtensions
    {
        public static readonly int EverySecond = 1000;
        public static readonly int EveryUpdate = 1001;
        public static readonly int OnSim1000ms = 1000;
        public static readonly int OnSim200ms = 200;
        public static readonly int OnSim100ms = 100;
        public static readonly int CycleStart = 1002;
        public static readonly int MinionDied = 1003;
        public static readonly int StressChanged = 1004;
        public static readonly int BuildingComplete = 1005;
    }

    // =====================================================================================================
    // PART 2: STUBS - Fallback implementations for types not in game assemblies
    // These are used when the game doesn't provide certain types.
    // =====================================================================================================

    /// <summary>
    /// STUB: Sim/Render scheduler simulation for standalone execution.
    /// </summary>
    public static class ModSimAndRenderScheduler
    {
        private static List<ScheduledEvent> _events = new List<ScheduledEvent>();

        public static void RegisterRepeating(float intervalSeconds, System.Action action)
        {
            _events.Add(new ScheduledEvent
            {
                Interval = intervalSeconds,
                Action = action,
                LastTime = Time.time
            });
        }

        public static void Unregister(System.Action action)
        {
            _events.RemoveAll(e => e.Action == action);
        }

        public static void Update()
        {
            float currentTime = Time.time;
            foreach (var evt in _events)
            {
                if (currentTime - evt.LastTime >= evt.Interval)
                {
                    evt.Action?.Invoke();
                    evt.LastTime = currentTime;
                }
            }
        }

        private class ScheduledEvent
        {
            public float Interval;
            public System.Action Action;
            public float LastTime;
        }
    }

    // =====================================================================================================
    // PART 3: GAME TYPE SHIMS - Provide missing game types
    // These types are NOT in the game assemblies and need to be defined.
    // =====================================================================================================

    #region Grid Utilities - Mod-owned utility class

    /// <summary>
    /// UTILITY: Grid utilities for spatial operations.
    /// This is a Mod-owned utility that wraps the game's Grid type.
    /// </summary>
    public static class GridRadialUtil
    {
        public static List<int> GetCellsInRadius(int centerCell, int radius)
        {
            var result = new List<int>();
            if (!global::Grid.IsValidCell(centerCell)) return result;

            int centerX, centerY;
            global::Grid.CellToXY(centerCell, out centerX, out centerY);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        int cell = global::Grid.XYToCell(centerX + dx, centerY + dy);
                        if (global::Grid.IsValidCell(cell))
                            result.Add(cell);
                    }
                }
            }
            return result;
        }
    }

    #endregion

    #region Notification System - Mod-owned types

    /// <summary>
    /// ENUM: Notification priority values for Mod notifications.
    /// </summary>
    public enum NotificationPriority
    {
        Boring = 0,
        Bad = 1,
        BadWithSound = 2,
        Good = 3,
        GoodWithSound = 4
    }

    #endregion

}

// =====================================================================================================
// PART 4: WRAPPERS - Types that MUST not conflict with game types
// These are prefixed to avoid naming conflicts.
// =====================================================================================================

namespace ONIModPack.Core.Compat.Wrappers
{
    using ONIModPack.Core.Compat;

    /// <summary>
    /// WRAPPER: BuildingDef wrapper to avoid conflicts.
    /// Use this when game BuildingDef is not available.
    /// </summary>
    [Obsolete("Use game types when available. This stub is for compatibility only.")]
    public class BuildingDefWrapper : ScriptableObject
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Anim = "";
        public int WidthInCells = 1;
        public int HeightInCells = 1;
        public string[] Categories = Array.Empty<string>();
        public string TechRequired = "";
        public float Mass = 100f;
        public float Decor = 0f;
        public bool RequiresPower = false;
        public float PowerConsumption = 0f;
    }

    /// <summary>
    /// WRAPPER: KAnim file wrapper.
    /// </summary>
    [Obsolete("Use game types when available.")]
    public class KAnimFileWrapper : ScriptableObject
    {
        public new string name = "";
    }
}

// =====================================================================================================
// PART 5: ADAPTERS - Safe wrappers for game types
// =====================================================================================================

namespace ONIModPack.Core.Compat.Adapters
{
    using UnityEngine;

    /// <summary>
    /// ADAPTER: Provides extension methods for Unity GameObject.
    /// Safe to use - does not define new types.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Safely gets component or returns null.
        /// </summary>
        public static T GetComponentSafe<T>(this GameObject go) where T : Component
        {
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// Adds component or returns existing.
        /// </summary>
        public static T AddOrGetComponent<T>(this GameObject go) where T : Component
        {
            if (go == null) return null;
            var component = go.GetComponent<T>();
            return component ?? go.AddComponent<T>();
        }
    }

    /// <summary>
    /// ADAPTER: Provides extension methods for Unity Transform.
    /// </summary>
    public static class TransformExtensions
    {
        public static Vector3 GetPosition(this Transform t) => t?.position ?? Vector3.zero;
    }
}
