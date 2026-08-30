using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ONIModPack.Core
{
    public enum EventPriority
    {
        Critical = 0,
        High = 10,
        Normal = 20,
        Low = 30,
        Background = 40
    }

    public static class PriorityEventBus
    {
        private static volatile HandlerNode[][] _handlers = new HandlerNode[0][];
        private static readonly ConcurrentDictionary<Type, int> _typeIndexMap = new ConcurrentDictionary<Type, int>();
        private static int _typeCount;
        private static readonly object _writeLock = new object();

        private sealed class HandlerNode
        {
            public readonly Delegate Delegate;
            public readonly EventPriority Priority;

            public HandlerNode(Delegate handler, EventPriority priority)
            {
                Delegate = handler;
                Priority = priority;
            }
        }

        public static void Subscribe<T>(Action<T> handler, EventPriority priority = EventPriority.Normal) where T : IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            int index = _typeIndexMap.GetOrAdd(type, _ => Interlocked.Increment(ref _typeCount) - 1);

            lock (_writeLock)
            {
                EnsureCapacity(index);
                HandlerNode[] snapshot = Volatile.Read(ref _handlers[index]);
                int length = snapshot.Length;
                var newHandlers = new HandlerNode[length + 1];
                Array.Copy(snapshot, newHandlers, length);
                newHandlers[length] = new HandlerNode(handler, priority);
                Array.Sort(newHandlers, (a, b) => a.Priority.CompareTo(b.Priority));
                Volatile.Write(ref _handlers[index], newHandlers);
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
                HandlerNode[] snapshot = Volatile.Read(ref _handlers[index]);
                int length = snapshot.Length;
                if (length == 0) return;

                if (length == 1)
                {
                    Volatile.Write(ref _handlers[index], new HandlerNode[0]);
                    return;
                }

                var newHandlers = new HandlerNode[length - 1];
                int writeIdx = 0;
                for (int i = 0; i < length; i++)
                {
                    if (!EqualityComparer<Delegate>.Default.Equals(snapshot[i].Delegate, handler))
                    {
                        newHandlers[writeIdx++] = snapshot[i];
                    }
                }
                Volatile.Write(ref _handlers[index], newHandlers);
            }
        }

        public static void Publish<T>(T evt) where T : IGameEvent
        {
            if (evt == null) return;

            var type = typeof(T);
            if (!_typeIndexMap.TryGetValue(type, out int index)) return;
            if (index >= _handlers.Length) return;

            HandlerNode[] snapshot = Volatile.Read(ref _handlers[index]);
            if (snapshot.Length == 0) return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<T>)snapshot[i].Delegate)(evt);
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.Logger.Error($"[PriorityEventBus] {type.Name}: {ex.Message}");
                }
            }
        }

        public static void Clear()
        {
            lock (_writeLock)
            {
                _handlers = new HandlerNode[0][];
                _typeIndexMap.Clear();
                _typeCount = 0;
            }
        }

        public static int GetSubscriberCount<T>() where T : IGameEvent
        {
            var type = typeof(T);
            if (!_typeIndexMap.TryGetValue(type, out int index)) return 0;
            if (index >= _handlers.Length) return 0;
            return Volatile.Read(ref _handlers[index]).Length;
        }

        public static void SubscribeWithOrder<T>(Action<T> handler, ExecutionOrder order) where T : IGameEvent
        {
            Subscribe(handler, order.ToEventPriority());
        }

        private static void EnsureCapacity(int index)
        {
            if (index < _handlers.Length) return;

            int newSize = index + 16;
            var newHandlers = new HandlerNode[newSize][];
            for (int i = 0; i < _handlers.Length; i++)
            {
                newHandlers[i] = Volatile.Read(ref _handlers[i]);
            }
            _handlers = newHandlers;
        }
    }

    public enum ExecutionOrder
    {
        SocialState = 0,
        Faction = 10,
        Law = 20,
        UI = 30
    }

    public static class ExecutionOrderExtensions
    {
        public static EventPriority ToEventPriority(this ExecutionOrder order)
        {
            switch (order)
            {
                case ExecutionOrder.SocialState:
                    return EventPriority.Critical;
                case ExecutionOrder.Faction:
                    return EventPriority.High;
                case ExecutionOrder.Law:
                    return EventPriority.Normal;
                case ExecutionOrder.UI:
                    return EventPriority.Low;
                default:
                    return EventPriority.Normal;
            }
        }
    }
}

