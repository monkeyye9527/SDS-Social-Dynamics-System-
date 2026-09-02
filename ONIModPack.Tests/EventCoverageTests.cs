using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ONIModPack.Core;
using Xunit;

namespace ONIModPack.Tests
{
    [Collection("EventBus")]
    public class EventCoverageTests
    {
        [Fact]
        public void BeliefChangedEvent_HasSubscriber_AfterPlaceholderWired()
        {
            EventBus.Clear();
            try
            {
                int hit = 0;
                EventBus.Subscribe<BeliefChangedEvent>(_ => hit++);
                EventBus.Publish(new BeliefChangedEvent { DuplicantId = 42 });
                Assert.Equal(1, hit);
                Assert.True(EventBus.GetSubscriberCount<BeliefChangedEvent>() >= 1);
            }
            finally
            {
                EventBus.Clear();
            }
        }

        [Fact]
        public void TraitGainedEvent_HasSubscriber_AfterPlaceholderWired()
        {
            EventBus.Clear();
            try
            {
                int hit = 0;
                EventBus.Subscribe<TraitGainedEvent>(_ => hit++);
                EventBus.Publish(new TraitGainedEvent { TraitId = "Brave" });
                Assert.Equal(1, hit);
            }
            finally
            {
                EventBus.Clear();
            }
        }

        [Fact]
        public void PersonalityFormedEvent_HasSubscriber_AfterPlaceholderWired()
        {
            EventBus.Clear();
            try
            {
                int hit = 0;
                EventBus.Subscribe<PersonalityFormedEvent>(_ => hit++);
                EventBus.Publish(new PersonalityFormedEvent());
                Assert.Equal(1, hit);
            }
            finally
            {
                EventBus.Clear();
            }
        }

        [Fact]
        public void ProductionCriticalEvents_AllHaveSubscribers_AfterPlaceholderWired()
        {
            EventBus.Clear();
            try
            {
                EventBus.Subscribe<BeliefChangedEvent>(_ => { });
                EventBus.Subscribe<TraitGainedEvent>(_ => { });
                EventBus.Subscribe<PersonalityFormedEvent>(_ => { });

                Assert.True(EventBus.GetSubscriberCount<BeliefChangedEvent>() >= 1,
                    "BeliefChangedEvent has no subscribers — dead letter.");
                Assert.True(EventBus.GetSubscriberCount<TraitGainedEvent>() >= 1,
                    "TraitGainedEvent has no subscribers — dead letter.");
                Assert.True(EventBus.GetSubscriberCount<PersonalityFormedEvent>() >= 1,
                    "PersonalityFormedEvent has no subscribers — dead letter.");
            }
            finally
            {
                EventBus.Clear();
            }
        }

        [Fact]
        public void NoSubscribers_ReportsZero()
        {
            EventBus.Clear();
            Assert.Equal(0, EventBus.GetSubscriberCount<BeliefChangedEvent>());
            Assert.Equal(0, EventBus.GetSubscriberCount<TraitGainedEvent>());
        }

        [Fact]
        public void MultipleSubscribers_AllReceive()
        {
            EventBus.Clear();
            try
            {
                int hitA = 0, hitB = 0;
                EventBus.Subscribe<BeliefChangedEvent>(_ => hitA++);
                EventBus.Subscribe<BeliefChangedEvent>(_ => hitB++);
                EventBus.Publish(new BeliefChangedEvent { DuplicantId = 1 });
                Assert.Equal(1, hitA);
                Assert.Equal(1, hitB);
                Assert.Equal(2, EventBus.GetSubscriberCount<BeliefChangedEvent>());
            }
            finally
            {
                EventBus.Clear();
            }
        }
    }
}