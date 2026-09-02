using System.Collections.Generic;
using System.Linq;
using Xunit;
using ONIModPack.Content.Behavior;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeDirectionResolverTests
    {
        [Fact]
        public void EmptySignals_AllTagsEqualWeight()
        {
            var config = new SocialTreeConfig { SWPressure = 0.5f, SWFaction = 0.3f, SWBehavior = 0.2f };
            var weights = SocialTreeDirectionResolver.ComputeWeights(
                new Dictionary<SituationAxis, float>(),
                new Dictionary<FactionType, float>(),
                new Dictionary<BehaviorType, int>(),
                config);

            Assert.Equal(1.0f, weights.Values.Sum(), 3);
            foreach (var w in weights.Values)
                Assert.InRange(w, 0.18f, 0.22f);
        }

        [Fact]
        public void HighLaborTension_GivesLaborHighWeight()
        {
            var config = new SocialTreeConfig { SWPressure = 0.5f, SWFaction = 0.3f, SWBehavior = 0.2f };
            var pressures = new Dictionary<SituationAxis, float>
            {
                [SituationAxis.LaborTension] = 0.8f,
                [SituationAxis.SocialUnrest] = 0.2f,
                [SituationAxis.IdeologicalSplit] = 0.1f,
                [SituationAxis.MoralFatigue] = 0.1f
            };

            var weights = SocialTreeDirectionResolver.ComputeWeights(
                pressures, new Dictionary<FactionType, float>(), new Dictionary<BehaviorType, int>(), config);

            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Culture]);
            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Economy]);
            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Governance]);
            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Mixed]);
        }

        [Fact]
        public void UnionHighInfluence_GivesLaborHighWeight()
        {
            var config = new SocialTreeConfig { SWPressure = 0.5f, SWFaction = 0.3f, SWBehavior = 0.2f };
            var factions = new Dictionary<FactionType, float>
            {
                [FactionType.Union] = 0.8f,
                [FactionType.Engineering] = 0.2f,
                [FactionType.Science] = 0.1f,
                [FactionType.Belief] = 0.1f
            };

            var weights = SocialTreeDirectionResolver.ComputeWeights(
                new Dictionary<SituationAxis, float>(), factions, new Dictionary<BehaviorType, int>(), config);

            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Culture]);
            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Economy]);
        }

        [Fact]
        public void ManyWorkBehavior_GivesLaborHighWeight()
        {
            var config = new SocialTreeConfig { SWPressure = 0.5f, SWFaction = 0.3f, SWBehavior = 0.2f };
            var behavior = new Dictionary<BehaviorType, int>
            {
                [BehaviorType.Work] = 10,
                [BehaviorType.Explore] = 5,
                [BehaviorType.Create] = 2,
                [BehaviorType.Socialize] = 3
            };

            var weights = SocialTreeDirectionResolver.ComputeWeights(
                new Dictionary<SituationAxis, float>(), new Dictionary<FactionType, float>(), behavior, config);

            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Culture]);
            Assert.True(weights[SocialTag.Labor] > weights[SocialTag.Economy]);
        }

        [Fact]
        public void Sum_IsAlwaysOne_MixedSignals()
        {
            var config = new SocialTreeConfig { SWPressure = 0.3f, SWFaction = 0.3f, SWBehavior = 0.4f };
            var pressures = new Dictionary<SituationAxis, float> { [SituationAxis.LaborTension] = 0.5f };
            var factions = new Dictionary<FactionType, float> { [FactionType.Belief] = 0.7f };
            var behavior = new Dictionary<BehaviorType, int> { [BehaviorType.Work] = 5 };

            var weights = SocialTreeDirectionResolver.ComputeWeights(pressures, factions, behavior, config);

            Assert.Equal(1.0f, weights.Values.Sum(), 3);
        }

        // ---- Tag 映射（Rushed 副作用用） ----
        [Fact]
        public void TagToAxis_MapsCorrectly()
        {
            Assert.Equal(SituationAxis.IdeologicalSplit, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Culture));
            Assert.Equal(SituationAxis.LaborTension, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Labor));
            Assert.Equal(SituationAxis.SocialUnrest, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Economy));
            Assert.Equal(SituationAxis.MoralFatigue, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Governance));
        }

        [Fact]
        public void TagToTopic_MapsCorrectly()
        {
            Assert.Equal(OpinionTopic.Religion, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Culture));
            Assert.Equal(OpinionTopic.WorkConditions, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Labor));
            Assert.Equal(OpinionTopic.Economy, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Economy));
            Assert.Equal(OpinionTopic.Welfare, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Governance));
        }

        [Fact]
        public void TagToFaction_MapsCorrectly()
        {
            Assert.Equal(FactionType.Belief, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Culture));
            Assert.Equal(FactionType.Union, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Labor));
            Assert.Equal(FactionType.Science, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Economy));
            Assert.Equal(FactionType.Engineering, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Governance));
        }
    }
}