using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ONIModPack.Core
{
    public sealed class SubscriptionToken : IEquatable<SubscriptionToken>
    {
        private static long _counter;

        public long Id { get; }
        public Type EventType { get; }
        public Delegate OriginalDelegate { get; }

        internal SubscriptionToken(Type eventType, Delegate originalDelegate)
        {
            Id = Interlocked.Increment(ref _counter);
            EventType = eventType;
            OriginalDelegate = originalDelegate;
        }

        public bool Equals(SubscriptionToken other) => other != null && Id == other.Id;
        public override bool Equals(object obj) => obj is SubscriptionToken other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(SubscriptionToken a, SubscriptionToken b) => a?.Id == b?.Id;
        public static bool operator !=(SubscriptionToken a, SubscriptionToken b) => !(a == b);
    }

    public static class EventBus
    {
        private static volatile HandlerSlot[][] _handlers = new HandlerSlot[0][];
        private static readonly ConcurrentDictionary<Type, int> _typeIndexMap = new ConcurrentDictionary<Type, int>();
        private static int _typeCount;
        private static readonly object _writeLock = new object();
        private static readonly ConcurrentDictionary<long, SubscriptionToken> _tokens = new ConcurrentDictionary<long, SubscriptionToken>();

        private sealed class HandlerSlot
        {
            public readonly SubscriptionToken Token;
            public readonly Action<IGameEvent> Handler;

            public HandlerSlot(SubscriptionToken token, Action<IGameEvent> handler)
            {
                Token = token;
                Handler = handler;
            }
        }

        public static SubscriptionToken Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            int index = _typeIndexMap.GetOrAdd(type, _ => Interlocked.Increment(ref _typeCount) - 1);

            var token = new SubscriptionToken(type, handler);

            lock (_writeLock)
            {
                EnsureCapacity(index);
                HandlerSlot[] snapshot = Volatile.Read(ref _handlers[index]) ?? Array.Empty<HandlerSlot>();
                int length = snapshot.Length;
                var newSlots = new HandlerSlot[length + 1];
                Array.Copy(snapshot, newSlots, length);
                newSlots[length] = new HandlerSlot(token, evt => handler((T)evt));
                Volatile.Write(ref _handlers[index], newSlots);
            }

            _tokens[token.Id] = token;
            return token;
        }

        public static void Unsubscribe(SubscriptionToken token)
        {
            if (token == null) return;

            var type = token.EventType;
            if (!_typeIndexMap.TryGetValue(type, out int index)) return;

            lock (_writeLock)
            {
                if (index >= _handlers.Length) return;
                HandlerSlot[] snapshot = Volatile.Read(ref _handlers[index]) ?? Array.Empty<HandlerSlot>();
                int length = snapshot.Length;
                if (length == 0) return;

                if (length == 1)
                {
                    Volatile.Write(ref _handlers[index], Array.Empty<HandlerSlot>());
                    TryRemoveToken(token);
                    return;
                }

                var newSlots = new HandlerSlot[length - 1];
                int writeIdx = 0;
                for (int i = 0; i < length; i++)
                {
                    if (snapshot[i].Token != token)
                    {
                        newSlots[writeIdx++] = snapshot[i];
                    }
                }
                Volatile.Write(ref _handlers[index], newSlots);
                TryRemoveToken(token);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            var type = typeof(T);
            if (!_typeIndexMap.TryGetValue(type, out int index)) return;

            lock (_writeLock)
            {
                if (index >= _handlers.Length) return;
                HandlerSlot[] snapshot = Volatile.Read(ref _handlers[index]) ?? Array.Empty<HandlerSlot>();
                int length = snapshot.Length;
                if (length == 0) return;

                int matchIdx = -1;
                for (int i = 0; i < length; i++)
                {
                    if (Delegate.Equals(snapshot[i].Token.OriginalDelegate, handler))
                    {
                        matchIdx = i;
                        break;
                    }
                }

                if (matchIdx < 0) return;

                if (length == 1)
                {
                    Volatile.Write(ref _handlers[index], Array.Empty<HandlerSlot>());
                    TryRemoveToken(snapshot[0].Token);
                    return;
                }

                var newSlots = new HandlerSlot[length - 1];
                int writeIdx = 0;
                for (int i = 0; i < length; i++)
                {
                    if (i != matchIdx)
                    {
                        newSlots[writeIdx++] = snapshot[i];
                    }
                }
                Volatile.Write(ref _handlers[index], newSlots);
                TryRemoveToken(snapshot[matchIdx].Token);
            }
        }

        public static void Publish<T>(T evt) where T : IGameEvent
        {
            if (evt == null) return;

            var type = typeof(T);
            if (!_typeIndexMap.TryGetValue(type, out int index)) return;
            if (index >= _handlers.Length) return;

            HandlerSlot[] snapshot = Volatile.Read(ref _handlers[index]);
            if (snapshot.Length == 0) return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler(evt);
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.Logger.Error($"[EventBus] {type.Name}: {ex.Message}");
                }
            }
        }

        public static void Clear()
        {
            lock (_writeLock)
            {
                _handlers = new HandlerSlot[0][];
                _typeIndexMap.Clear();
                _typeCount = 0;
                _tokens.Clear();
            }
        }

        public static int GetSubscriberCount<T>() where T : IGameEvent
        {
            var type = typeof(T);
            if (!_typeIndexMap.TryGetValue(type, out int index)) return 0;
            if (index >= _handlers.Length) return 0;
            return Volatile.Read(ref _handlers[index]).Length;
        }

        private static void EnsureCapacity(int index)
        {
            if (index < _handlers.Length) return;

            int newSize = index + 16;
            var newHandlers = new HandlerSlot[newSize][];
            for (int i = 0; i < _handlers.Length; i++)
            {
                newHandlers[i] = Volatile.Read(ref _handlers[i]);
            }
            _handlers = newHandlers;
        }

        private static void TryRemoveToken(SubscriptionToken token)
        {
            if (token != null)
            {
                _tokens.TryRemove(token.Id, out _);
            }
        }
    }

    public interface IGameEvent
    {
        float GameTime { get; }
        int DuplicantId { get; }
    }

    public abstract class GameEvent : IGameEvent
    {
        public float GameTime { get; set; }
        public int DuplicantId { get; set; } = -1;

        protected GameEvent() => GameTime = UnityEngine.Time.time;
    }

    public class BeliefChangedEvent : GameEvent
    {
        public int BeliefType;
        public float OldValue, NewValue;
        public bool IsConflict;
        public float Intensity;
    }

    public class BeliefConflictEvent : GameEvent
    {
        public int BeliefA, BeliefB;
        public float Intensity;
    }

    public class StressChangedEvent : GameEvent
    {
        public float OldStress, NewStress;
        public int Level;
    }

    public class StressBreakdownEvent : GameEvent
    {
        public float StressValue;
    }

    public class StrikeStartedEvent : GameEvent
    {
        public int RoomId;
        public int Count;
    }

    public class StrikeEndedEvent : GameEvent
    {
        public int RoomId;
        public float Duration;
    }

    public class TraitGainedEvent : GameEvent
    {
        public string TraitId;
    }

    public class TraitLostEvent : GameEvent
    {
        public string TraitId;
    }

    public class SocialInteractionEvent : GameEvent
    {
        public int TargetId;
        public float Outcome;
    }

    public class PersonalityFormedEvent : GameEvent
    {
        public float Aggression, Conformity, Empathy, Rationality, Fanaticism;
    }

    public class DuplicantSpawnedEvent : GameEvent { }
    public class DuplicantDiedEvent : GameEvent { }

    public class ModuleStateChangedEvent : GameEvent
    {
        public string ModuleId;
        public bool IsEnabled;
    }

    public class CycleChangedEvent : GameEvent
    {
        public int CycleNumber;
    }

    public class InteractionStartedEvent : GameEvent
    {
        public int MinionId;
        public float[] Position;
        public string InteractionType;

        public InteractionStartedEvent(int minionId, float x, float y, string interactionType)
        {
            MinionId = minionId;
            Position = new[] { x, y };
            InteractionType = interactionType;
        }
    }

    public class InteractionEndedEvent : GameEvent
    {
        public int MinionId;
        public float[] Position;
        public string InteractionType;

        public InteractionEndedEvent(int minionId, float x, float y, string interactionType)
        {
            MinionId = minionId;
            Position = new[] { x, y };
            InteractionType = interactionType;
        }
    }

    public class BuildingPlacedEvent : GameEvent
    {
        public string BuildingId;
        public string BuildingName;
        public int Cell;
        public float X;
        public float Y;
    }

    public class BuildingRemovedEvent : GameEvent
    {
        public string BuildingId;
        public string BuildingName;
        public int Cell;
    }

    // =====================================================================
    // Mod / game lifecycle events — see GameLifecycleHooks.cs for where these
    // are published, and ModuleBase.cs for a convenient base class that wires
    // a module's OnGameLoaded/OnWorldLoaded/OnWorldUnloaded/OnPreSave/OnPostSave
    // overrides to these automatically.
    //
    // Firing order within a single game session:
    //   ModLoadedEvent   -> once, end of ModEntry.OnLoad (this mod's own patches/
    //                       modules are registered; other mods may not be yet)
    //   GameLoadedEvent  -> once, end of ModEntry.OnAllModsLoaded (every mod is
    //                       loaded and every IModModule.Initialize() has run)
    //   WorldLoadedEvent -> every time a save/new game finishes loading
    //   PreSaveEvent     -> every time a save is about to be written
    //   PostSaveEvent    -> every time a save finishes writing
    //   WorldUnloadedEvent -> every time the current world is torn down
    //                         (return to main menu, load a different save, quit)
    // =====================================================================

    /// <summary>This mod's assembly has loaded and its own modules/patches are
    /// registered. Fires once. DuplicantId/GameTime are not meaningful for this event.</summary>
    public class ModLoadedEvent : GameEvent { }

    /// <summary>Every installed mod has loaded and every IModModule.Initialize() has
    /// run. Fires once. Good place for cross-module wiring that needs all modules present.</summary>
    public class GameLoadedEvent : GameEvent { }

    /// <summary>A save finished loading (or a new game started) and the colony is live.
    /// Fires on every load, not just the first — reset any per-save state here.</summary>
    public class WorldLoadedEvent : GameEvent { }

    /// <summary>The current world is about to be torn down. Pairs with WorldLoadedEvent.</summary>
    public class WorldUnloadedEvent : GameEvent { }

    /// <summary>The game is about to write a save file. Push any in-memory state that
    /// needs to survive the save into your serialized fields here.</summary>
    public class PreSaveEvent : GameEvent { }

    /// <summary>The game just finished writing a save file.</summary>
    public class PostSaveEvent : GameEvent { }
}

