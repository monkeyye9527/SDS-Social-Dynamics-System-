using System.Collections.Generic;
using Xunit;
using ONIModPack.Core;
using ONIModPack.Content.Legislation.Model;
using ONIModPack.Content.Legislation.Runtime;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.SocialResearch;

// 类型 Legislation 与命名空间 ONIModPack.Content.Legislation 同名，避免解析为命名空间（CS0118）。
using LawInstance = ONIModPack.Content.Legislation.Model.Legislation;

namespace ONIModPack.Tests
{
    /// <summary>
    /// 存档兼容性回归（旧档升级 + 存两轮）。
    /// 覆盖用户关心的四大状态中的可纯逻辑测试部分：法律（LegislationRegistry）、
    /// 研究树进度（SocialResearchUnlocks）；派系/信仰因依赖游戏运行时（GameClock/Unity），须进游戏手工回归。
    /// </summary>
    public class SaveCompatRoundTripTests
    {
        // ---- 旧档升级：缺 v0.90/v0.91/v0.92 新增键时必须以字段默认值落位，不得抛异常 ----

        [Fact]
        public void Legislation_OldSaveMissingV091Fields_LoadsWithDefaults()
        {
            // 模拟 v0.89 存档：仅含基础字段，无 Level/EnforcementMode/PassiveEffects/ParentLawId/RejectReason/AuthorityCost
            var oldSave = new Dictionary<string, object>
            {
                ["Laws"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["Id"] = "L1",
                        ["Name"] = "Min Wage",
                        ["TemplateId"] = "Labor",
                        ["Status"] = (int)LawStatus.Enacted,
                        ["Parameters"] = new Dictionary<string, object> { ["MinWage"] = 10 },
                    }
                }
            };

            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.Load(oldSave); // 旧档升级不得抛异常

            var law = registry.GetLaw("L1");
            Assert.NotNull(law);
            Assert.Equal(LawStatus.Enacted, law.Status);
            Assert.Equal(LawLevel.PoliticalLaw, law.Level);                 // 缺键 → 字段初始值
            Assert.Equal(EnforcementModel.AgencyRequired, law.EnforcementMode);
            Assert.Equal(0f, law.AuthorityCost);
            Assert.Null(law.ParentLawId);
            Assert.Null(law.RejectReason);
            Assert.NotNull(law.PassiveEffects);
            Assert.Empty(law.PassiveEffects);
            Assert.NotNull(law.Rules);
            Assert.Empty(law.Rules);
        }

        [Fact]
        public void Research_OldSaveMissingLastInterveneCycle_FallsBackToMinus100()
        {
            // 模拟旧档：无 LastInterveneCycle（InterveneNode 冷却周期为 v0.90 后新增）
            var oldSave = new Dictionary<string, object>
            {
                ["UnlockedResearch"] = new List<string> { "CommunityDevelopment" },
                ["UnlockedBuildings"] = new List<string> { "CommunityCenter" },
            };

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            unlocks.Load(oldSave);

            Assert.True(unlocks.IsResearchUnlocked("CommunityDevelopment"));
            Assert.True(unlocks.IsBuildingUnlocked("CommunityCenter"));

            // 冷却周期缺省 → -100（-100 < 任意正常周期，等效无冷却）
            var data = new Dictionary<string, object>();
            unlocks.Save(data);
            Assert.Equal(-100, data["LastInterveneCycle"]);
        }

        // ---- 存两轮：Save→Load→Save→Load 无数据丢失、无重复实体 ----

        [Fact]
        public void Legislation_TwoSaveCycles_Stable()
        {
            var reg0 = new LegislationRegistry();
            reg0.Initialize();
            reg0.RegisterLaw(BuildV092Law());

            var data1 = new Dictionary<string, object>();
            reg0.Save(data1);

            // 第 1 轮载入 + 再存
            var reg1 = new LegislationRegistry();
            reg1.Initialize();
            reg1.Load(data1);
            var data2 = new Dictionary<string, object>();
            reg1.Save(data2);

            // 第 2 轮载入 → 校验完整性与幂等性
            var reg2 = new LegislationRegistry();
            reg2.Initialize();
            reg2.Load(data2);

            var got = reg2.GetLaw("L1");
            Assert.NotNull(got);
            Assert.Single(reg2.GetAllLaws()); // 不重复
            Assert.Equal(LawStatus.Enacted, got.Status);
            Assert.Equal(LawLevel.PoliticalLaw, got.Level);
            Assert.Equal(EnforcementModel.AgencyRequired, got.EnforcementMode);
            Assert.Equal(1.5f, got.AuthorityCost);
            Assert.Equal("P9", got.ParentLawId);
            Assert.Equal("理由不充分", got.RejectReason);
            Assert.Equal("p1", got.SourceProblemId);
            Assert.Equal("g1", got.SponsorGroupId);
            Assert.Single(got.PassiveEffects);
            Assert.Equal(LawEffectChannel.PressureAxis, got.PassiveEffects[0].Channel);
            Assert.Equal("LaborTension", got.PassiveEffects[0].Target);
            Assert.Equal(0.01f, got.PassiveEffects[0].MagnitudePerSecond);
            Assert.Equal("MinWage", got.PassiveEffects[0].ScaleByParameter);
            Assert.Single(got.Rules);
            Assert.Equal("V1", got.Rules[0].ViolationId);
            Assert.Equal(10, got.Parameters["MinWage"].Value);
        }

        [Fact]
        public void Research_TwoSaveCycles_Stable()
        {
            var data1 = new Dictionary<string, object>
            {
                ["UnlockedResearch"] = new List<string> { "A", "B" },
                ["UnlockedBuildings"] = new List<string> { "PremiumBuilding" },
                ["LastInterveneCycle"] = 42,
            };

            var u1 = new SocialResearchUnlocks();
            u1.Initialize();
            u1.Load(data1);
            var data2 = new Dictionary<string, object>();
            u1.Save(data2);

            var u2 = new SocialResearchUnlocks();
            u2.Initialize();
            u2.Load(data2);
            var data3 = new Dictionary<string, object>();
            u2.Save(data3);

            Assert.True(u2.IsResearchUnlocked("A"));
            Assert.True(u2.IsResearchUnlocked("B"));
            Assert.True(u2.IsBuildingUnlocked("PremiumBuilding"));
            Assert.Equal(42, data3["LastInterveneCycle"]); // 冷却周期跨轮保留
        }

        // ---- SaveDataManager 编排层：多系统并存两轮 ----

        [Fact]
        public void SaveDataManager_RegisteredSystems_TwoCycles_Survive()
        {
            var manager = new SaveDataManager();
            manager.Initialize();

            // 第 1 轮前置状态
            var lawReg = new LegislationRegistry();
            lawReg.Initialize();
            lawReg.RegisterLaw(BuildV092Law());
            manager.RegisterSaveable(lawReg);

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            unlocks.Load(new Dictionary<string, object>
            {
                ["UnlockedResearch"] = new List<string> { "LaborOrganization" },
                ["UnlockedBuildings"] = new List<string> { },
                ["LastInterveneCycle"] = 7,
            });
            manager.RegisterSaveable(unlocks);

            manager.OnSave(); // 快照 1

            // 模拟重进游戏：全新系统从快照恢复
            var lawReg2 = new LegislationRegistry();
            lawReg2.Initialize();
            manager.RegisterSaveable(lawReg2);
            var unlocks2 = new SocialResearchUnlocks();
            unlocks2.Initialize();
            manager.RegisterSaveable(unlocks2);
            manager.OnLoad();

            manager.OnSave(); // 快照 2（第二轮保存，验证恢复后可继续保存）

            var lawReg3 = new LegislationRegistry();
            lawReg3.Initialize();
            manager.RegisterSaveable(lawReg3);
            var unlocks3 = new SocialResearchUnlocks();
            unlocks3.Initialize();
            manager.RegisterSaveable(unlocks3);
            manager.OnLoad();

            // 两轮后状态仍完整
            var law = lawReg3.GetLaw("L1");
            Assert.NotNull(law);
            Assert.Equal("Min Wage", law.Name);
            Assert.Equal(1.5f, law.AuthorityCost);
            Assert.True(unlocks3.IsResearchUnlocked("LaborOrganization"));
            var saved = new Dictionary<string, object>();
            unlocks3.Save(saved);
            Assert.Equal(7, saved["LastInterveneCycle"]);
        }

        // ---- PoliticalGroup 持久化（v0.92b）：组织 Id 被法律 SponsorGroupId 引用，跨档必须稳定 ----

        [Fact]
        public void PoliticalGroups_OldSaveMissingGroupsKey_LoadsEmptyWithoutThrow()
        {
            // v0.92 之前的旧档：无 Groups 键 → 空表落位，不得抛异常
            var factory = new PoliticalGroupFactory();
            factory.Initialize();
            factory.Load(new Dictionary<string, object>());

            Assert.Empty(factory.Groups);
        }

        [Fact]
        public void PoliticalGroups_TwoSaveCycles_Stable()
        {
            var f0 = new PoliticalGroupFactory();
            f0.Initialize();
            var g0 = f0.GetOrCreate(SituationAxis.LaborTension, "矿工", 0.8f, "issue-1");
            g0.Influence = 0.42f;
            g0.Popularity = 0.6f;
            g0.Organization = 0.75f;
            g0.AddParticipant("dup-1");
            g0.AddParticipant("dup-2");
            g0.IssuePositions["LaborTension"] = 0.9f;

            var data1 = new Dictionary<string, object>();
            f0.Save(data1);

            // 第 1 轮载入 + 再存
            var f1 = new PoliticalGroupFactory();
            f1.Initialize();
            f1.Load(data1);
            var data2 = new Dictionary<string, object>();
            f1.Save(data2);

            // 第 2 轮载入
            var f2 = new PoliticalGroupFactory();
            f2.Initialize();
            f2.Load(data2);

            var g = Assert.Single(f2.Groups); // 不重复
            Assert.Equal(g0.Id, g.Id);        // Id 跨轮稳定 → 存续法律 SponsorGroupId 归因不断裂
            Assert.Equal(SituationAxis.LaborTension, g.SourceAxis);
            Assert.Equal("issue-1", g.SourceIssue);
            Assert.Equal(0.42f, g.Influence, 4);
            Assert.Equal(0.6f, g.Popularity, 4);
            Assert.Equal(0.75f, g.Organization, 4);
            Assert.Equal(new[] { "dup-1", "dup-2" }, g.Participants);
            Assert.Equal(0.9f, g.IssuePositions["LaborTension"], 4);
            Assert.NotEmpty(g.Goals);

            // 复用语义保持：同键 GetOrCreate 命中恢复后的实例并累计组织度（不新建组织）
            var reused = f2.GetOrCreate(SituationAxis.LaborTension, "矿工", 0.8f, "issue-1");
            Assert.Same(g, reused);
            Assert.Equal(0.8f, reused.Organization, 3);
        }

        [Fact]
        public void PoliticalGroups_LoadWithJsonPromotedDoubles_Survives()
        {
            // 真实存档 JSON 落盘后：float → double、List<Dictionary<string,float>> 值类型提升为非泛型字典。
            // Load 必须宽容解析，否则 Influence/Organization 等静默回退默认值（组织状态丢失）。
            var data = new Dictionary<string, object>
            {
                ["Groups"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["Key"] = "LaborTension:矿工",
                        ["Id"] = "grp-1",
                        ["Name"] = "劳资紧张·矿工",
                        ["SourceAxis"] = (int)SituationAxis.LaborTension,
                        ["SourceIssue"] = "issue-1",
                        ["CoalitionId"] = null,
                        ["FoundedAt"] = 120.0,          // double
                        ["ParentFaction"] = -1,
                        ["Influence"] = 0.42,           // double
                        ["Popularity"] = 0.6,           // double
                        ["Organization"] = 0.75,        // double
                        ["Goals"] = new List<string> { "解决劳资紧张问题" },
                        ["IssuePositions"] = new Dictionary<string, object> { ["LaborTension"] = 0.9 }, // 值提升为 double
                        ["Participants"] = new List<string> { "dup-1" },
                    }
                }
            };

            var factory = new PoliticalGroupFactory();
            factory.Initialize();
            factory.Load(data); // double 提升存档加载不得丢失数值

            var g = Assert.Single(factory.Groups);
            Assert.Equal("grp-1", g.Id);
            Assert.Equal(SituationAxis.LaborTension, g.SourceAxis);
            Assert.Equal(0.42f, g.Influence, 4);
            Assert.Equal(0.6f, g.Popularity, 4);
            Assert.Equal(0.75f, g.Organization, 4);
            Assert.Equal(120.0f, g.FoundedAt, 4);
            Assert.Null(g.ParentFaction); // -1 哨兵 → 不恢复
            Assert.Equal(0.9f, g.IssuePositions["LaborTension"], 4);
        }

        private static LawInstance BuildV092Law()
        {
            var law = new LawInstance
            {
                Id = "L1",
                Name = "Min Wage",
                TemplateId = "Labor",
                Status = LawStatus.Enacted,
                Level = LawLevel.PoliticalLaw,
                EnforcementMode = EnforcementModel.AgencyRequired,
                AuthorityCost = 1.5f,
                ParentLawId = "P9",
                RejectReason = "理由不充分",
                SourceProblemId = "p1",
                SponsorGroupId = "g1",
            };
            law.Parameters["MinWage"] = new ParameterValue { ParameterId = "MinWage", Value = 10 };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Description = "加班违规" });
            law.PassiveEffects.Add(new LawEffectDefinition
            {
                Channel = LawEffectChannel.PressureAxis,
                Target = "LaborTension",
                MagnitudePerSecond = 0.01f,
                ScaleByParameter = "MinWage",
                ParamScale = 0.5f,
            });
            return law;
        }
    }
}