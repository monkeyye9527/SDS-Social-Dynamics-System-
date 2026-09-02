using System;
using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Config;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialDynamics.Governance;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.SocialDynamics.Emergence;

namespace ONIModPack.Tests
{
    public class ReliefConfigProblemDefaultsTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var cfg = new ReliefConfig(); // 不调 Initialize：默认值即为代码默认

            Assert.Equal(0.002f, cfg.ProblemLegitimacyWearPolitical);
            Assert.Equal(0.005f, cfg.ProblemLegitimacyWearCrisis);
            Assert.Equal(0.0005f, cfg.ProblemOpinionWearPolitical);
            Assert.Equal(0.001f, cfg.ProblemOpinionWearCrisis);
            Assert.Equal(0.01f, cfg.ProblemResolvedLegitimacyRise);
            Assert.Equal(0.01f, cfg.ProblemResolvedOpinionRise);
            Assert.Equal(0.8f, cfg.ProblemCooldownDiscountCommon);
            Assert.Equal(0.6f, cfg.ProblemCooldownDiscountPolitical);
            Assert.Equal(0.5f, cfg.ProblemCooldownDiscountCrisis);
            Assert.Equal(0.02f, cfg.ProblemBudgetRegenPolitical);
            Assert.Equal(0.05f, cfg.ProblemBudgetRegenCrisis);
        }
    }

    public class ReliefConfigProblemOverrideTests
    {
        private class FakeConfigManager : IConfigManager
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public string ConfigDirectory => "fake";
            public bool IsInitialized { get; private set; }
            public event Action<string> OnConfigChanged;
            public bool IsModuleEnabled(string moduleId, bool defaultValue = false) => defaultValue;
            public void SetModuleEnabled(string moduleId, bool enabled) { }
            public T GetConfig<T>(string moduleId, string key, T defaultValue = default) where T : class => defaultValue;
            public T GetConfigValue<T>(string moduleId, string key, T defaultValue = default)
                => Values.TryGetValue(moduleId + "." + key, out var v) ? (T)v : defaultValue;
            public void SetConfigValue<T>(string moduleId, string key, T value) => Values[moduleId + "." + key] = value;
            public void Load() { }
            public void Save() { }
            public void Reload() { }
            public void Initialize() => IsInitialized = true;
            public void Shutdown() => IsInitialized = false;
            public void Dispose() { }
        }

        [Fact]
        public void Initialize_ReadsOverride_FromConfigManager()
        {
            ServiceRegistry.Clear();
            var fake = new FakeConfigManager();
            fake.SetConfigValue("Content.Governance", "Relief.Problem.CooldownDiscountCrisis", 0.25f);
            ServiceRegistry.Register<IConfigManager>(fake);

            var cfg = new ReliefConfig();
            cfg.Initialize();

            Assert.Equal(0.25f, cfg.ProblemCooldownDiscountCrisis);
            Assert.Equal(0.6f, cfg.ProblemCooldownDiscountPolitical); // 未覆盖键保持默认
            ServiceRegistry.Clear();
        }
    }

    public class ProblemResolvedEventSourceAxisTests
    {
        [Fact]
        public void Event_HasSourceAxis_AfterConstruction()
        {
            var e = new ONIModPack.Content.SocialDynamics.Emergence.ProblemResolvedEvent
            {
                SourceAxis = ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension
            };
            Assert.Equal(ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, e.SourceAxis);
        }
    }

    public class LegitimacyEmergentWearTests
    {
        private static ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel lvl)
            => new ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem { Level = lvl, IsActive = true };

        [Fact] public void Empty_ReturnsZero()
            => Assert.Equal(0f, Legitimacy.GetEmergentWearRatePerSec(null, new ReliefConfig()));

        [Fact] public void NullConfig_ReturnsZero()
            => Assert.Equal(0f, Legitimacy.GetEmergentWearRatePerSec(new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis) }, null));

        [Fact] public void CommonTrendLocal_ReturnZero()
            => Assert.Equal(0f, Legitimacy.GetEmergentWearRatePerSec(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Common), P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Trend), P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Local) },
                new ReliefConfig()));

        [Fact] public void SinglePolitical_ReturnsPoliticalRate()
            => Assert.Equal(0.002f, Legitimacy.GetEmergentWearRatePerSec(new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Political) }, new ReliefConfig()));

        [Fact] public void SingleCrisis_ReturnsCrisisRate()
            => Assert.Equal(0.005f, Legitimacy.GetEmergentWearRatePerSec(new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis) }, new ReliefConfig()));

        [Fact] public void MultiAxis_IndependentStacking()
            => Assert.Equal(0.007f, Legitimacy.GetEmergentWearRatePerSec(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Political), P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis) },
                new ReliefConfig()));

        [Fact] public void InactiveProblem_Ignored()
            => Assert.Equal(0f, Legitimacy.GetEmergentWearRatePerSec(
                new[] { new ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem { Level = ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, IsActive = false } },
                new ReliefConfig()));
    }

    public class GovernanceEmergentPureTests
    {
        private static ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel lvl, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis axis)
            => new ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem { Level = lvl, IsActive = true, SourceAxis = axis };

        [Fact] public void BudgetRegen_None_Zero()
            => Assert.Equal(0f, GovernanceResponseSystem.GetEmergentBudgetRegenPerSec(null, new ReliefConfig()));

        [Fact] public void BudgetRegen_Common_Zero()
            => Assert.Equal(0f, GovernanceResponseSystem.GetEmergentBudgetRegenPerSec(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Common, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) }, new ReliefConfig()));

        [Fact] public void BudgetRegen_Political_002()
            => Assert.Equal(0.02f, GovernanceResponseSystem.GetEmergentBudgetRegenPerSec(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Political, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) }, new ReliefConfig()));

        [Fact] public void BudgetRegen_Crisis_005()
            => Assert.Equal(0.05f, GovernanceResponseSystem.GetEmergentBudgetRegenPerSec(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) }, new ReliefConfig()));

        [Fact] public void CooldownDiscount_NoProblem_One()
            => Assert.Equal(1f, GovernanceResponseSystem.GetAxisCooldownDiscount(null, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, new ReliefConfig()));

        [Fact] public void CooldownDiscount_OtherAxis_One()
            => Assert.Equal(1f, GovernanceResponseSystem.GetAxisCooldownDiscount(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.MoralFatigue) },
                ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, new ReliefConfig()));

        [Fact] public void CooldownDiscount_Common_08()
            => Assert.Equal(0.8f, GovernanceResponseSystem.GetAxisCooldownDiscount(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Common, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) },
                ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, new ReliefConfig()));

        [Fact] public void CooldownDiscount_Political_06()
            => Assert.Equal(0.6f, GovernanceResponseSystem.GetAxisCooldownDiscount(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Political, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) },
                ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, new ReliefConfig()));

        [Fact] public void CooldownDiscount_Crisis_05()
            => Assert.Equal(0.5f, GovernanceResponseSystem.GetAxisCooldownDiscount(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) },
                ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension, new ReliefConfig()));
    }

    public class PublicOpinionEmergentErosionTests
    {
        private static ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel lvl, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis axis)
            => new ONIModPack.Content.SocialDynamics.Emergence.EmergentProblem { Level = lvl, IsActive = true, SourceAxis = axis };

        [Fact]
        public void PoliticalLaborTension_ErodesWorkConditionsByRateTimesInterval()
        {
            var map = PublicOpinionSystem.GetEmergentOpinionErosion(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Political, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.LaborTension) },
                new ReliefConfig(), 60f);
            Assert.Equal(-0.0005f * 60f, map[OpinionTopic.WorkConditions], 5);
        }

        [Fact]
        public void CrisisSocialUnrest_ErodesLawAtCrisisRate()
        {
            var map = PublicOpinionSystem.GetEmergentOpinionErosion(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.SocialUnrest) },
                new ReliefConfig(), 60f);
            Assert.Equal(-0.001f * 60f, map[OpinionTopic.Law], 5);
        }

        [Fact]
        public void LawTopic_ReflectsGlobalMaxLevel_EvenWhenOtherAxis()
        {
            var map = PublicOpinionSystem.GetEmergentOpinionErosion(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Crisis, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.MoralFatigue) },
                new ReliefConfig(), 60f);
            Assert.Equal(-0.001f * 60f, map[OpinionTopic.Entertainment], 5);
            Assert.Equal(-0.001f * 60f, map[OpinionTopic.Law], 5);
        }

        [Fact]
        public void CommonProblem_NoErosion()
        {
            var map = PublicOpinionSystem.GetEmergentOpinionErosion(
                new[] { P(ONIModPack.Content.SocialDynamics.Emergence.ProblemLevel.Common, ONIModPack.Content.SocialDynamics.Emergence.SituationAxis.SocialUnrest) },
                new ReliefConfig(), 60f);
            Assert.Empty(map);
        }

        [Fact]
        public void Empty_ReturnsEmpty()
            => Assert.Empty(PublicOpinionSystem.GetEmergentOpinionErosion(null, new ReliefConfig(), 60f));
    }

    public class EmergentProblemWireupTests
    {
        [Fact]
        public void HandleResolved_WithNoServicesRegistered_DoesNotThrow()
        {
            ServiceRegistry.Clear();
            var ex = Record.Exception(() => EmergentProblemWireup.HandleResolved(new ProblemResolvedEvent
            {
                ProblemId = "p1",
                Description = "d",
                SourceAxis = SituationAxis.LaborTension
            }));
            Assert.Null(ex);
        }
    }
}
