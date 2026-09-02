using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.Legislation.Model;
using ONIModPack.Content.Legislation.Runtime;
using ONIModPack.Content.SocialDynamics;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.SocialDynamics.Emergence;
using LawInstance = ONIModPack.Content.Legislation.Model.Legislation;

namespace ONIModPack.Tests
{
    public class LegislationTraceFieldsTests
    {
        [Fact]
        public void NewLaw_TraceFields_DefaultNull()
        {
            var law = new Legislation();
            Assert.Null(law.SourceProblemId);
            Assert.Null(law.SponsorGroupId);
        }

        [Fact]
        public void SaveLoad_RoundTrips_TraceFields()
        {
            var registry = new LegislationRegistry();
            registry.RegisterLaw(new Legislation
            {
                Id = "l1",
                Name = "n",
                TemplateId = "t",
                SourceProblemId = "p9",
                SponsorGroupId = "g3",
            });
            var data = new Dictionary<string, object>();
            registry.Save(data);
            registry.Load(data);
            var loaded = registry.GetLaw("l1");
            Assert.NotNull(loaded);
            Assert.Equal("p9", loaded.SourceProblemId);
            Assert.Equal("g3", loaded.SponsorGroupId);
        }

        [Fact]
        public void Load_LegacyEntryWithoutTraceFields_LoadsNulls()
        {
            var registry = new LegislationRegistry();
            var data = new Dictionary<string, object>
            {
                ["Laws"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { ["Id"] = "l1", ["Name"] = "n", ["TemplateId"] = "t" }
                }
            };
            registry.Load(data);
            var loaded = registry.GetLaw("l1");
            Assert.NotNull(loaded);
            Assert.Null(loaded.SourceProblemId);
            Assert.Null(loaded.SponsorGroupId);
        }
    }

    public class LawStakeholderTests
    {
        [Fact]
        public void Template_DefaultStakeholders_Empty()
        {
            var t = new LawTemplate();
            Assert.NotNull(t.Stakeholders);
            Assert.Empty(t.Stakeholders);
        }

        [Fact]
        public void Stakeholder_Defaults()
        {
            var s = new LawStakeholder();
            Assert.Equal(1, s.Direction);
            Assert.Equal(0.3f, s.Amplitude);
        }
    }

    public class EmergenceConfigLawGeneratorDefaultsTests
    {
        [Fact]
        public void LawGeneratorDefaults_MatchSpec()
        {
            var cfg = new ONIModPack.Content.SocialDynamics.Emergence.EmergenceConfig();
            Assert.Equal(60f, cfg.EmergentCandidateCommonSeconds);
            Assert.Equal(120f, cfg.EmergentSubmitPoliticalSeconds);
            Assert.Equal(180f, cfg.EmergentResubmitCooldownSeconds);
            Assert.Equal(120f, cfg.EmergentGlobalSubmitCooldownSeconds);
            Assert.Equal(0.5f, cfg.EmergentLawPassThreshold);
            Assert.Equal(8, cfg.EmergentLawCapacity);
        }
    }

    public class PoliticalGroupFactoryTests
    {
        [Fact]
        public void SameAxisGroupKey_ReturnsSameInstance_AndAccumulatesOrg()
        {
            var f = new PoliticalGroupFactory();
            var g1 = f.GetOrCreate(SituationAxis.LaborTension, "工人", 0.8f, "p1");
            var g2 = f.GetOrCreate(SituationAxis.LaborTension, "工人", 0.8f, "p2");
            Assert.Same(g1, g2);
            Assert.Equal(0.1f + 0.8f * 0.2f + 0.05f, g2.Organization, 5);
        }

        [Fact]
        public void DifferentKey_CreatesNewGroup_WithSourceIssue()
        {
            var f = new PoliticalGroupFactory();
            var g = f.GetOrCreate(SituationAxis.MoralFatigue, "医疗人员", 0.5f, "p9");
            Assert.NotNull(g.Id);
            Assert.Equal("p9", g.SourceIssue);
            Assert.Equal(SituationAxis.MoralFatigue, g.SourceAxis); // v0.92：创建时永久绑定关切轴
            Assert.Single(g.Goals);
        }
    }

    public class EmergentLawGeneratorPureTests
    {
        [Fact] public void MapAxis_LaborTension_ToLabor()
            => Assert.Equal(LawCategory.Labor, EmergentLawGenerator.MapAxisToLawCategory(SituationAxis.LaborTension));

        [Fact] public void MapAxis_SocialUnrest_ToSecurity()
            => Assert.Equal(LawCategory.Security, EmergentLawGenerator.MapAxisToLawCategory(SituationAxis.SocialUnrest));

        [Fact] public void MapAxis_IdeologicalSplit_ToReligion()
            => Assert.Equal(LawCategory.Religion, EmergentLawGenerator.MapAxisToLawCategory(SituationAxis.IdeologicalSplit));

        [Fact] public void MapAxis_MoralFatigue_ToWelfare()
            => Assert.Equal(LawCategory.Welfare, EmergentLawGenerator.MapAxisToLawCategory(SituationAxis.MoralFatigue));

        [Fact] public void Modulate_Severity05_Identity()
            => Assert.Equal(10f, EmergentLawGenerator.ModulateParameter(10f, 0.5f), 5);

        [Fact] public void Modulate_Severity10_Upscales125()
            => Assert.Equal(12.5f, EmergentLawGenerator.ModulateParameter(10f, 1.0f), 5);

        [Fact] public void Modulate_Severity00_Downscales075()
            => Assert.Equal(7.5f, EmergentLawGenerator.ModulateParameter(10f, 0.0f), 5);

        [Fact] public void Negotiate_UpDirection_FullSeverity_AppliesFullAmplitude()
            => Assert.Equal(13f, EmergentLawGenerator.NegotiateParameter(10f, new LawStakeholder { Direction = 1, Amplitude = 0.3f }, 1.0f), 5);

        [Fact] public void Negotiate_DownDirection_HalfSeverity_HalfForce()
            => Assert.Equal(10f * (1f - 1 * 0.3f * 0.75f), EmergentLawGenerator.NegotiateParameter(10f, new LawStakeholder { Direction = -1, Amplitude = 0.3f }, 0.5f), 5);
    }

    public class ComputeSupportTests
    {
        [Fact] public void Severity10_NoBias_FullSupport()
            => Assert.Equal(1f, EmergentLawPipeline.ComputeSupport(new[] { "a", "b", "c" }, 1f, 0f), 5);

        [Fact] public void Severity04_NoBias_BelowThreshold()
            => Assert.True(EmergentLawPipeline.ComputeSupport(new[] { "a" }, 0.4f, 0f) < 0.5f);

        [Fact] public void Severity04_WithBias_Passes()
            => Assert.Equal(0.55f, EmergentLawPipeline.ComputeSupport(new[] { "a" }, 0.4f, 0.15f), 5);

        [Fact]
        public void DebatingThenVoting_ReachesVerdict_WithInjectedClock()
        {
            var pipeline = new EmergentLawPipeline { DebugNow = 100f };
            pipeline.Initialize(); // 解析 registry/ruling（可空）
            var law = new LawInstance { Id = "l", Name = "n", Status = LawStatus.Proposed };
            var problem = new EmergentProblem
            {
                Id = "p",
                SourceAxis = SituationAxis.SocialUnrest,
                Severity = 0.9f,
                IsActive = true,
                AffectedGroups = new[] { "居民" }
            };
            pipeline.Submit(law, problem);
            pipeline.DebugNow = 131f; // Debating 30s 到期 → 进 Voting
            pipeline.Tick(1f);
            pipeline.DebugNow = 162f; // Voting 30s 到期 → 裁决
            pipeline.Tick(1f);
            Assert.True(law.Status == LawStatus.Enacted || law.Status == LawStatus.Rejected);
            pipeline.Shutdown();
        }
    }

    public class EmergentLawGeneratorGuardTests
    {
        private static EmergentProblem Pol(string id = "p1", SituationAxis axis = SituationAxis.LaborTension)
            => new EmergentProblem
            {
                Id = id,
                SourceAxis = axis,
                Level = ProblemLevel.Political,
                Persistence = 200f,
                Severity = 0.8f,
                IsActive = true,
            };

        private static EmergenceConfig Cfg() => new EmergenceConfig();

        [Fact]
        public void ShouldSubmit_LevelBelowPolitical_False()
        {
            var p = Pol();
            p.Level = ProblemLevel.Common;
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                new Dictionary<SituationAxis, float>(), false, 0, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_PersistenceBelowThreshold_False()
        {
            var p = Pol();
            p.Persistence = 60f;
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                new Dictionary<SituationAxis, float>(), false, 0, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_AlreadySubmittedAxis_False()
        {
            var p = Pol();
            var submitted = new HashSet<SituationAxis> { SituationAxis.LaborTension };
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, submitted,
                new Dictionary<SituationAxis, float>(), false, 0, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_PendingForAxis_False()
        {
            var p = Pol();
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                new Dictionary<SituationAxis, float>(), true, 0, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_CapacityFull_False()
        {
            var p = Pol();
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                new Dictionary<SituationAxis, float>(), false, 8, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_CrisisResubmit_SubmittedAxis_WithinCooldown_False()
        {
            var p = Pol();
            p.Level = ProblemLevel.Crisis;
            var submitted = new HashSet<SituationAxis> { SituationAxis.LaborTension };
            var rejected = new Dictionary<SituationAxis, float> { [SituationAxis.LaborTension] = 100f };
            Assert.False(EmergentLawGenerator.ShouldSubmitStatic(p, submitted,
                rejected, false, 0, 8, 200f, Cfg())); // Crisis: 轴已提交, now(200)-rejected(100)=100 < 180 → 冷却未过不可重提
        }

        [Fact]
        public void ShouldSubmit_RejectedAfterCooldown_True()
        {
            var p = Pol();
            var rejected = new Dictionary<SituationAxis, float> { [SituationAxis.LaborTension] = 100f };
            Assert.True(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                rejected, false, 0, 8, 281f, Cfg())); // now(281) - rejected(100) = 181 >= 180
        }

        [Fact]
        public void ShouldSubmit_CrisisResubmit_SubmittedAxis_AfterCooldown_True()
        {
            var p = Pol();
            p.Level = ProblemLevel.Crisis;
            var submitted = new HashSet<SituationAxis> { SituationAxis.LaborTension };
            var rejected = new Dictionary<SituationAxis, float> { [SituationAxis.LaborTension] = 100f };
            Assert.True(EmergentLawGenerator.ShouldSubmitStatic(p, submitted,
                rejected, false, 0, 8, 281f, Cfg())); // Crisis: 轴已在 submittedAxes 且 now(281)-rejected(100)=181 >= 180 → 可重提
        }

        [Fact]
        public void ShouldSubmit_Normal_True()
        {
            var p = Pol();
            Assert.True(EmergentLawGenerator.ShouldSubmitStatic(p, new HashSet<SituationAxis>(),
                new Dictionary<SituationAxis, float>(), false, 0, 8, 0f, Cfg()));
        }

        [Fact]
        public void ShouldSubmit_CapacityCountsOnlyEnacted_DraftsIgnored()
        {
            // 回归：主管线在 Draft 状态即注册法律，未生效的草稿/审批中法律不得占用 8 条容量。
            // 7 条已生效 + 1 条草稿 → 有效容量 7 < 8，应允许提交。
            ServiceRegistry.Clear();
            try
            {
                var registry = new LegislationRegistry();
                for (int i = 0; i < 7; i++)
                {
                    registry.RegisterLaw(new LawInstance
                    {
                        Id = "e" + i, Name = "n", TemplateId = "t",
                        Level = LawLevel.PoliticalLaw, Status = LawStatus.Enacted,
                    });
                }
                registry.RegisterLaw(new LawInstance
                {
                    Id = "draft1", Name = "n", TemplateId = "t",
                    Level = LawLevel.PoliticalLaw, Status = LawStatus.Draft,
                });

                ServiceRegistry.Register<LegislationRegistry>(registry);
                var gen = new EmergentLawGenerator { DebugNow = 100f };
                gen.Initialize();
                var p = Pol();
                Assert.True(gen.ShouldSubmit(p));
                gen.Shutdown();
            }
            finally
            {
                ServiceRegistry.Clear();
            }
        }

        [Fact]
        public void ShouldSubmit_CapacityFilledByEnacted_RepealedExcluded_True()
        {
            // 8 条已生效 = 容量 → 待容量，即使同时存在已废止法律也不放行；废止一条后恢复。
            ServiceRegistry.Clear();
            try
            {
                var registry = new LegislationRegistry();
                for (int i = 0; i < 8; i++)
                {
                    registry.RegisterLaw(new LawInstance
                    {
                        Id = "e" + i, Name = "n", TemplateId = "t",
                        Level = LawLevel.PoliticalLaw, Status = LawStatus.Enacted,
                    });
                }
                registry.RegisterLaw(new LawInstance
                {
                    Id = "old1", Name = "n", TemplateId = "t",
                    Level = LawLevel.PoliticalLaw, Status = LawStatus.Repealed,
                });

                ServiceRegistry.Register<LegislationRegistry>(registry);
                var gen = new EmergentLawGenerator { DebugNow = 100f };
                gen.Initialize();

                var p = Pol();
                Assert.False(gen.ShouldSubmit(p)); // 8 = 容量 → 阻塞

                registry.GetLaw("e0").Status = LawStatus.Repealed; // 玩家废止 1 条
                Assert.True(gen.ShouldSubmit(p));                    // 7 < 8 → 恢复提交
                gen.Shutdown();
            }
            finally
            {
                ServiceRegistry.Clear();
            }
        }

        [Fact]
        public void Tick_Smoke_NoThrowWhenRegistered()
        {
            ServiceRegistry.Clear();
            try
            {
                ServiceRegistry.Register<LegislationRegistry>(new LegislationRegistry());
                ServiceRegistry.Register<PoliticalGroupFactory>(new PoliticalGroupFactory());
                ServiceRegistry.Register<EmergenceConfig>(new EmergenceConfig());
                ServiceRegistry.Register<EmergentLawPipeline>(new EmergentLawPipeline());

                var gen = new EmergentLawGenerator { DebugNow = 100f };
                gen.Initialize();
                gen.DebugNow = 200f;
                gen.Tick(1f); // 无活跃问题 → 不抛异常（冒烟）
                gen.Shutdown();
            }
            finally
            {
                ServiceRegistry.Clear();
            }
        }
    }
}
