using System;
using ONIModPack.Core;
using Xunit;

namespace ONIModPack.Tests
{
    [Collection("EventBus")]
    public class EventBusTests
    {
        [Fact]
        public void SubscribeAndPublishEvent()
        {
            EventBus.Clear();
            int receivedCount = 0;
            BeliefChangedEvent receivedEvent = null;

            EventBus.Subscribe<BeliefChangedEvent>(evt =>
            {
                receivedCount++;
                receivedEvent = evt;
            });

            Assert.Equal(1, EventBus.GetSubscriberCount<BeliefChangedEvent>());

            var testEvent = new BeliefChangedEvent
            {
                BeliefType = 1,
                OldValue = 10f,
                NewValue = 20f,
                IsConflict = true
            };

            EventBus.Publish(testEvent);

            Assert.Equal(1, receivedCount);
            Assert.NotNull(receivedEvent);
            Assert.Equal(1, receivedEvent.BeliefType);
            Assert.Equal(20f, receivedEvent.NewValue);
        }

        [Fact]
        public void MultipleSubscribersAllReceiveEvent()
        {
            EventBus.Clear();
            int subscriber1Count = 0;
            int subscriber2Count = 0;

            EventBus.Subscribe<BeliefChangedEvent>(_ => subscriber1Count++);
            EventBus.Subscribe<BeliefChangedEvent>(_ => subscriber2Count++);

            Assert.Equal(2, EventBus.GetSubscriberCount<BeliefChangedEvent>());

            EventBus.Publish(new BeliefChangedEvent());

            Assert.Equal(1, subscriber1Count);
            Assert.Equal(1, subscriber2Count);
        }

        [Fact]
        public void UnsubscribeByTokenRemovesSubscriber()
        {
            EventBus.Clear();
            int receivedCount = 0;

            var token = EventBus.Subscribe<BeliefChangedEvent>(_ => receivedCount++);

            EventBus.Publish(new BeliefChangedEvent());
            Assert.Equal(1, receivedCount);

            EventBus.Unsubscribe(token);

            EventBus.Publish(new BeliefChangedEvent());
            Assert.Equal(1, receivedCount);
            Assert.Equal(0, EventBus.GetSubscriberCount<BeliefChangedEvent>());
        }

        [Fact]
        public void UnsubscribeByHandlerRemovesSubscriber()
        {
            EventBus.Clear();
            var counter = new Counter();
            Action<BeliefChangedEvent> handler = counter.Increment;

            EventBus.Subscribe(handler);

            EventBus.Publish(new BeliefChangedEvent());
            Assert.Equal(1, counter.Value);

            EventBus.Unsubscribe(handler);

            EventBus.Publish(new BeliefChangedEvent());
            Assert.Equal(1, counter.Value);
        }

        [Fact]
        public void ClearRemovesAllSubscribers()
        {
            EventBus.Clear();

            EventBus.Subscribe<BeliefChangedEvent>(_ => { });
            EventBus.Subscribe<StressChangedEvent>(_ => { });

            Assert.Equal(1, EventBus.GetSubscriberCount<BeliefChangedEvent>());
            Assert.Equal(1, EventBus.GetSubscriberCount<StressChangedEvent>());

            EventBus.Clear();

            Assert.Equal(0, EventBus.GetSubscriberCount<BeliefChangedEvent>());
            Assert.Equal(0, EventBus.GetSubscriberCount<StressChangedEvent>());
        }

        [Fact]
        public void PublishNullEventDoesNothing()
        {
            EventBus.Clear();
            int receivedCount = 0;

            EventBus.Subscribe<BeliefChangedEvent>(_ => receivedCount++);
            EventBus.Publish((BeliefChangedEvent)null);

            Assert.Equal(0, receivedCount);
        }

        [Fact]
        public void HandlerExceptionDoesNotBreakOtherHandlers()
        {
            EventBus.Clear();
            int successCount = 0;

            EventBus.Subscribe<BeliefChangedEvent>(_ => throw new InvalidOperationException("Test exception"));
            EventBus.Subscribe<BeliefChangedEvent>(_ => successCount++);

            EventBus.Publish(new BeliefChangedEvent());

            Assert.Equal(1, successCount);
        }

        [Fact]
        public void DifferentEventTypesAreIsolated()
        {
            EventBus.Clear();
            int beliefCount = 0;
            int stressCount = 0;

            EventBus.Subscribe<BeliefChangedEvent>(_ => beliefCount++);
            EventBus.Subscribe<StressChangedEvent>(_ => stressCount++);

            EventBus.Publish(new BeliefChangedEvent());
            Assert.Equal(1, beliefCount);
            Assert.Equal(0, stressCount);

            EventBus.Publish(new StressChangedEvent());
            Assert.Equal(1, beliefCount);
            Assert.Equal(1, stressCount);
        }

        private class Counter
        {
            public int Value { get; private set; }
            public void Increment(BeliefChangedEvent evt) => Value++;
        }
    }
}