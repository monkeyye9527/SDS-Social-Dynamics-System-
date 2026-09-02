using System;
using System.Collections.Generic;
using System.Linq;
using ONIModPack.Content.Legislation.Model;
using ONIModPack.Content.Legislation.Runtime;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Governance;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Core;
using ONIModPack.Core.Services;
using Xunit;

// 类型 Legislation 与命名空间 ONIModPack.Content.Legislation 同名，避免解析为命名空间（CS0118）。
using LawInstance = ONIModPack.Content.Legislation.Model.Legislation;

namespace ONIModPack.Tests
{
    /// <summary>
    /// v0.92 测试：PoliticalGroup 参与 FactionActionSystem 遍历（归因通道 + 扩权通道）。
    /// 单一定位规则：法律 PressureAxis 效果看向组织 SourceAxis 的净速率决定施压/声援。
    /// </summary>
    public class FactionActionSystemGroupWiringTests
    {
        /// <summary>最小 IFactionSystem 替身（复制自 LegislationTests，保证 pass1 不参与）。</summary>
        private class FakeFactionSystem : IFactionSystem
        {
            private readonly Dictionary<FactionType, FactionData> _factions = new Dictionary<FactionType, FactionData>();
            public event Action<FactionType, float> OnPopularityChanged;
            public event Action<FactionType, FactionLeader> OnLeaderChanged;
            public float PublicOpinionModifier { get; set; }
            public float PolarizationModifier { get; set; }

            public FakeFactionSystem(params FactionType[] types)
            {
                foreach (var t in types)
                    _factions[t] = new FactionData { Type = t, Popularity = 0.6f };
            }

            public FactionData GetFaction(FactionType type) => _factions.TryGetValue(type, out var fd) ? fd : null;
            public FactionLeader GetLeader(FactionType type) => null;
            public FactionType GetDominantFaction() => FactionType.Union;
            public void UpdatePopularity() { }
            public void AssignLeader(FactionType type, UnityEngine.GameObject duplicant) { }
            public void AddMember(FactionType type, string memberId) { }
            public void RemoveMember(FactionType type, string memberId) { }
            public FactionType GetDuplicantFaction(UnityEngine.GameObject duplicant) => FactionType.Engineering;
            public void Update() { }
            public void OnMinionDied() { }
            public void RestoreLeader(FactionType type, string leaderId) { }
            public void OnDuplicantBeliefChanged(int duplicantId, ONIModPack.Content.BeliefSystem.BeliefType newBelief) { }
            public bool IsInitialized { get; private set; }
            public void Initialize() => IsInitialized = true;
            public void Shutdown() => IsInitialized = false;
        }

        /// <summary>构造：注册工厂 + 一个组织（severity 0.5 → Organization 0.2）。</summary>
        private static PoliticalGroupFactory CreateFactoryWithGroup(SituationAxis axis, string label,
            out PoliticalGroup group, float severity = 0.5f)
        {
            var factory = new PoliticalGroupFactory();
            factory.Initialize();
            group = factory.GetOrCreate(axis, label, severity, "p1");
            ServiceRegistry.Register(factory);
            return factory;
        }

        /// <summary>构造：已生效法律 + 已注入工厂的 FactionActionSystem（Initialize 时解析工厂）。</summary>
        private static FactionActionSystem CreateSystem(LawInstance law, PoliticalGroupFactory factory)
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterLaw(law);

            var factions = new FakeFactionSystem();
            var system = new FactionActionSystem(factions, registry);
            system.Initialize();
            return system;
        }

        private static LawInstance EnactedPressureLaw(string id, SituationAxis axis, float magnitude)
        {
            var law = new LawInstance { Id = id, Name = "L", TemplateId = "Labor", Status = LawStatus.Enacted };
            law.PassiveEffects.Add(new LawEffectDefinition
            {
                Channel = LawEffectChannel.PressureAxis,
                Target = axis.ToString(),
                MagnitudePerSecond = magnitude
            });
            return law;
        }

        [Fact]
        public void GroupFactory_SetsSourceAxis_OnCreate()
        {
            var factory = new PoliticalGroupFactory();
            var g = factory.GetOrCreate(SituationAxis.MoralFatigue, "医疗人员", 0.5f, "p9");
            Assert.Equal(SituationAxis.MoralFatigue, g.SourceAxis); // 创建时永久绑定关切轴
        }

        [Fact]
        public void Group_AttributedAggravatingLaw_Pressures()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group);
                var law = EnactedPressureLaw("law_press", SituationAxis.LaborTension, 0.01f);
                law.SponsorGroupId = group.Id; // 归因：组织催生的法律

                var system = CreateSystem(law, factory);
                system.Tick(30f); // 归因速率 0.0066/s → 不满 0.198 > 阈值

                var action = Assert.Single(system.RecentActions.Where(a => a.GroupId != null));
                Assert.Equal(FactionActionType.PoliticalPressure, action.Type);
                Assert.Equal(group.Id, action.GroupId);
                Assert.Equal(group.Name, action.ActorName);
                Assert.True(group.Influence > 0.1f); // 施压成功后扩权
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_AttributedRelievingLaw_Endorses()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                // severity 1.0 → Organization 0.3，恰好过声援门槛（≥0.3）
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group, severity: 1.0f);
                var law = EnactedPressureLaw("law_endorse", SituationAxis.LaborTension, -0.001f);
                law.SponsorGroupId = group.Id; // 归因

                var system = CreateSystem(law, factory);
                system.Tick(1f);

                var action = Assert.Single(system.RecentActions.Where(a => a.GroupId != null));
                Assert.Equal(FactionActionType.PublicEndorsement, action.Type);
                Assert.True(group.Influence > 0.1f);  // 声援成功扩权
                Assert.True(group.Popularity > 0.3f); // 归因声援额外声望收益
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_NonAttributedSameAxisLaw_Pressures()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group);
                // 非归因：无 SponsorGroupId / SourceProblemId → 权重 1.0，同样触发（扩权通道）
                var law = EnactedPressureLaw("law_expand", SituationAxis.LaborTension, 0.01f);

                var system = CreateSystem(law, factory);
                system.Tick(40f); // 权重 1.0 → 速率 0.0044/s → 40s 不满 0.176 > 阈值

                var action = Assert.Single(system.RecentActions.Where(a => a.GroupId != null));
                Assert.Equal(FactionActionType.PoliticalPressure, action.Type);
                Assert.Equal(group.Id, action.GroupId);
                Assert.True(group.Influence > 0.1f);
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_DifferentAxisLaw_NoReaction()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out _);
                // 异轴法律：组织关切轴无 PressureAxis 效果 → 无表态
                var law = EnactedPressureLaw("law_other", SituationAxis.SocialUnrest, 0.01f);

                var system = CreateSystem(law, factory);
                system.Tick(60f);

                Assert.Empty(system.RecentActions);
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_Cooldown_LimitsReTriggerPerLaw()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group);
                var law = EnactedPressureLaw("law_cd", SituationAxis.LaborTension, 0.01f);
                law.SponsorGroupId = group.Id;

                var system = CreateSystem(law, factory);

                system.Tick(30f);  // 首次施压
                Assert.Single(system.RecentActions.Where(a => a.GroupId != null));

                system.Tick(30f);  // 距上次 <60s → 冷却拦截
                Assert.Single(system.RecentActions.Where(a => a.GroupId != null));

                system.Tick(70f);  // 距上次 ≥60s → 再次施压
                Assert.Equal(2, system.RecentActions.Count(a => a.GroupId != null));
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_AttributedFiresEarlierThanNonAttributed()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                // 同一工厂内两个组织：甲归因（权重 1.5），乙非归因（权重 1.0）
                var factory = new PoliticalGroupFactory();
                factory.Initialize();
                var groupA = factory.GetOrCreate(SituationAxis.LaborTension, "甲", 0.5f, "p1");
                var groupB = factory.GetOrCreate(SituationAxis.LaborTension, "乙", 0.5f, "p2");
                ServiceRegistry.Register(factory);
                var law = EnactedPressureLaw("law_w", SituationAxis.LaborTension, 0.01f);
                law.SponsorGroupId = groupA.Id; // 仅甲归因（权重 1.5）

                var system = CreateSystem(law, factory);
                system.Tick(30f); // A: 0.0066/s→0.198 触发；B: 0.0044/s→0.132 未到阈值

                var firedAfter30 = system.RecentActions.Where(a => a.GroupId != null).ToList();
                var action = Assert.Single(firedAfter30);
                Assert.Equal(groupA.Id, action.GroupId);

                system.Tick(40f); // B 追加累积 0.176 → 触发
                Assert.Contains(system.RecentActions, a => a.GroupId != null && a.GroupId == groupB.Id);
            }
            finally { ServiceRegistry.Clear(); }
        }

        // ---- v0.92 修复：政府对组织让步/镇压（组织不满 key 可被政府行动清零） ----

        [Fact]
        public void Group_GovernmentConcedeClearsGroupGrievance()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group);
                var law = EnactedPressureLaw("law_conc", SituationAxis.LaborTension, 0.01f);
                law.SponsorGroupId = group.Id; // 归因 → 权重 1.5

                var system = CreateSystem(law, factory);

                // 无累积不满时：让步失败（与派系 Concede 语义一致）
                Assert.False(system.ConcedeGroup(group.Id, law.Id));

                // 累积 10s（不满 0.099 < 阈值 0.15，未触发施压）→ 可让步
                system.Tick(10f);
                Assert.True(system.ConcedeGroup(group.Id, law.Id));

                // 让步清零后同时间再累积：不满重新从 0 起，10s 后仍未到阈值 → 无组织施压动作（政府让步动作带 GroupId 但 IsGovernmentAction）
                system.Tick(10f);
                Assert.DoesNotContain(system.RecentActions, a => a.GroupId != null && !a.IsGovernmentAction);
            }
            finally { ServiceRegistry.Clear(); }
        }

        [Fact]
        public void Group_GovernmentSuppressClearsGrievanceAndRequiresLegitimacy()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            try
            {
                var factory = CreateFactoryWithGroup(SituationAxis.LaborTension, "工人", out var group);
                var law = EnactedPressureLaw("law_sup", SituationAxis.LaborTension, 0.01f);
                law.SponsorGroupId = group.Id;

                var system = CreateSystem(law, factory);
                system.Tick(10f); // 累积不满但未触发

                // 未注册 Legitimacy → 镇压失败（合法性过低）
                Assert.False(system.SuppressGroup(group.Id, law.Id));

                // 注册高合法性 → 镇压成功
                var legitimacy = new Legitimacy();
                legitimacy.Initialize();
                ServiceRegistry.Register(legitimacy);
                Assert.True(system.SuppressGroup(group.Id, law.Id));

                // 镇压清零后同时间再累积 → 无组织施压动作（政府镇压动作带 GroupId 但 IsGovernmentAction）
                system.Tick(10f);
                Assert.DoesNotContain(system.RecentActions, a => a.GroupId != null && !a.IsGovernmentAction);
            }
            finally { ServiceRegistry.Clear(); }
        }
    }
}