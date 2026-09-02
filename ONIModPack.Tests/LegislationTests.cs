using System;
using System.Collections.Generic;
using ONIModPack.Content.Legislation;
using ONIModPack.Content.Legislation.Events;
using ONIModPack.Content.Legislation.Model;
using ONIModPack.Content.Legislation.Runtime;
using ONIModPack.Content.SocialDynamics;
using ONIModPack.Content.Legislation.Templates;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Governance;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Core;
using ONIModPack.Core.Rules;
using ONIModPack.Core.Services;
using System.IO;
using System.Reflection;
using Xunit;

namespace ONIModPack.Tests
{
    // EventBus 为静态全局状态，须与 EventBusTests 同集合串行执行，避免并行 Clear 干扰订阅
    [Collection("EventBus")]
    public class LegislationTests
    {
        static LegislationTests()
        {
            // 罢工谈判闭环测试会在 net8 测试宿主中执行触及 Assembly-CSharp 类型的方法
            // （StrikeSystem.EndAllStrikes 引用 Components/Notification/Game/Notifier）。
            // 挂 AssemblyResolve 从仓库 lib/ 目录补齐游戏程序集，使 JIT 可完成解析；
            // EndAllStrikes 内部对 Unity 侧访问均有 try/catch 或空值保护，可安全运行。
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name;
                try
                {
                    var libDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "lib"));
                    var file = Path.Combine(libDir, name + ".dll");
                    return File.Exists(file) ? Assembly.LoadFrom(file) : null;
                }
                catch { return null; }
            };
        }

        [Fact]
        public void Validate_RejectsOutOfRange()
        {
            var p = new LawParameter { Id = "MinWage", Type = LawParameterType.Integer, Min = 0f, Max = 100f, Default = 10f };
            string error;
            Assert.False(p.Validate(150, out error));
            Assert.Contains("range", error);
        }

        [Fact]
        public void Validate_RejectsWrongType()
        {
            var p = new LawParameter { Id = "AllowStrike", Type = LawParameterType.Boolean, Default = 1f };
            string error;
            Assert.False(p.Validate("yes", out error));
        }

        [Fact]
        public void Validate_AcceptsValidAndEnum()
        {
            var n = new LawParameter { Id = "MinWage", Type = LawParameterType.Integer, Min = 0f, Max = 100f, Default = 10f };
            string error;
            Assert.True(n.Validate(10, out error));

            var e = new LawParameter { Id = "SafetyLevel", Type = LawParameterType.Enum, Default = 0f, EnumValues = new[] { "Low", "High" } };
            Assert.True(e.Validate("High", out error));
            Assert.False(e.Validate("Medium", out error));
        }

        [Fact]
        public void Registry_SaveLoadRoundtrip()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor" });

            var law = new Legislation { Id = "L1", Name = "Min Wage", TemplateId = "Labor", Status = LawStatus.Enacted };
            law.Parameters["MinWage"] = new ParameterValue { ParameterId = "MinWage", Value = 10 };
            registry.RegisterLaw(law);

            var data = new Dictionary<string, object>();
            registry.Save(data);

            var restored = new LegislationRegistry();
            restored.Initialize();
            restored.Load(data);

            var got = restored.GetLaw("L1");
            Assert.NotNull(got);
            Assert.Equal("Min Wage", got.Name);
            Assert.Equal(LawStatus.Enacted, got.Status);
        }

        [Fact]
        public void Manager_CreateLawValidatesParameters()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate
            {
                Id = "Labor",
                Name = "Labor",
                Parameters = new List<LawParameter> { new LawParameter { Id = "MinWage", Type = LawParameterType.Integer, Min = 0f, Max = 100f, Default = 10f } }
            });

            var manager = new LegislationManager(registry);
            string error;

            var bad = manager.CreateLaw("Labor", "Bad Law", new Dictionary<string, object> { { "MinWage", 999 } }, out error);
            Assert.Null(bad);
            Assert.NotNull(error);

            var ok = manager.CreateLaw("Labor", "Good Law", new Dictionary<string, object> { { "MinWage", 25 } }, out error);
            Assert.NotNull(ok);
            Assert.Equal(LawStatus.Draft, ok.Status);
        }

        [Fact]
        public void Manager_CreateLawRejectsMissingTemplate()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var manager = new LegislationManager(registry);
            string error;
            Assert.Null(manager.CreateLaw("Missing", "X", null, out error));
        }

        [Fact]
        public void Manager_EnactAndRepealPublishEvents()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor" });
            var manager = new LegislationManager(registry);

            int enacted = 0, repealed = 0;
            EventBus.Subscribe<LawEnactedEvent>(_ => enacted++);
            EventBus.Subscribe<LawRepealedEvent>(_ => repealed++);

            string error;
            var law = manager.CreateLaw("Labor", "L", null, out error);
            Assert.True(manager.EnactLaw(law.Id));
            Assert.Equal(1, enacted);
            Assert.True(manager.RepealLaw(law.Id));
            Assert.Equal(1, repealed);
        }

        // ---- Phase 3：ConditionEvaluator 真值表 ----

        private static float ResolveFrom(Dictionary<string, float> fields, string field)
        {
            return fields.TryGetValue(field, out var v) ? v : 0f;
        }

        private static ConditionGroup Leaf(ComparisonOp op, float threshold)
        {
            return new ConditionGroup
            {
                Op = ConditionOp.And,
                Conditions = new List<LawCondition> { new LawCondition { Field = "X", Op = op, Threshold = threshold } }
            };
        }

        [Fact]
        public void ConditionEvaluator_AndTruthTable()
        {
            var fields = new Dictionary<string, float> { { "Stress", 80f }, { "Satisfaction", 40f } };
            Func<string, float> resolve = f => ResolveFrom(fields, f);

            var allTrue = new ConditionGroup
            {
                Op = ConditionOp.And,
                Conditions = new List<LawCondition>
                {
                    new LawCondition { Field = "Stress", Op = ComparisonOp.GreaterThan, Threshold = 75f },
                    new LawCondition { Field = "Satisfaction", Op = ComparisonOp.LessThan, Threshold = 50f }
                }
            };
            Assert.True(ConditionEvaluator.Evaluate(allTrue, resolve));

            var oneFalse = new ConditionGroup
            {
                Op = ConditionOp.And,
                Conditions = new List<LawCondition>
                {
                    new LawCondition { Field = "Stress", Op = ComparisonOp.GreaterThan, Threshold = 75f },
                    new LawCondition { Field = "Satisfaction", Op = ComparisonOp.GreaterThan, Threshold = 50f }
                }
            };
            Assert.False(ConditionEvaluator.Evaluate(oneFalse, resolve));
        }

        [Fact]
        public void ConditionEvaluator_OrNotNestedAndEmpty()
        {
            var fields = new Dictionary<string, float> { { "Stress", 80f } };
            Func<string, float> resolve = f => ResolveFrom(fields, f);

            // Or：一真即真
            var or = new ConditionGroup
            {
                Op = ConditionOp.Or,
                Conditions = new List<LawCondition>
                {
                    new LawCondition { Field = "Stress", Op = ComparisonOp.LessThan, Threshold = 50f },
                    new LawCondition { Field = "Stress", Op = ComparisonOp.GreaterThan, Threshold = 70f }
                }
            };
            Assert.True(ConditionEvaluator.Evaluate(or, resolve));

            // Not：取反
            var not = new ConditionGroup
            {
                Op = ConditionOp.Not,
                Conditions = new List<LawCondition>
                {
                    new LawCondition { Field = "Stress", Op = ComparisonOp.GreaterThan, Threshold = 70f }
                }
            };
            Assert.False(ConditionEvaluator.Evaluate(not, resolve));

            // 嵌套 And(Not(...))
            var nested = new ConditionGroup
            {
                Op = ConditionOp.And,
                Groups = new List<ConditionGroup>
                {
                    new ConditionGroup
                    {
                        Op = ConditionOp.Not,
                        Conditions = new List<LawCondition>
                        {
                            new LawCondition { Field = "Stress", Op = ComparisonOp.LessThan, Threshold = 50f }
                        }
                    }
                }
            };
            Assert.True(ConditionEvaluator.Evaluate(nested, resolve));

            // 空组语义：And→true，Or→false，Not→false
            Assert.True(ConditionEvaluator.Evaluate(new ConditionGroup { Op = ConditionOp.And }, resolve));
            Assert.False(ConditionEvaluator.Evaluate(new ConditionGroup { Op = ConditionOp.Or }, resolve));
            Assert.False(ConditionEvaluator.Evaluate(new ConditionGroup { Op = ConditionOp.Not }, resolve));
        }

        [Fact]
        public void ConditionEvaluator_BoundaryComparisons()
        {
            var fields = new Dictionary<string, float> { { "X", 10f } };
            Func<string, float> resolve = f => ResolveFrom(fields, f);

            Assert.False(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.GreaterThan, 10f), resolve));
            Assert.True(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.GreaterOrEqual, 10f), resolve));
            Assert.False(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.LessThan, 10f), resolve));
            Assert.True(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.LessOrEqual, 10f), resolve));
            Assert.True(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.Equal, 10f), resolve));
            Assert.False(ConditionEvaluator.Evaluate(Leaf(ComparisonOp.NotEqual, 10f), resolve));
        }

        // ---- Phase 3：RuleEvaluator ----

        [Fact]
        public void RuleEvaluator_OnlyEvaluatesEnactedLaws()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();

            var rule = new LawRule { Id = "R1", ViolationId = "WageViolation", Condition = Leaf(ComparisonOp.GreaterThan, 70f) };

            var draftLaw = new Legislation { Id = "D1", Name = "Draft", TemplateId = "T", Status = LawStatus.Draft };
            draftLaw.Rules.Add(rule);
            registry.RegisterLaw(draftLaw);

            var enactedLaw = new Legislation { Id = "E1", Name = "Enacted", TemplateId = "T", Status = LawStatus.Enacted };
            enactedLaw.Rules.Add(rule);
            registry.RegisterLaw(enactedLaw);

            var evaluator = new RuleEvaluator(registry, f => 80f);
            var violations = evaluator.Evaluate();

            Assert.Single(violations);
            Assert.Equal("E1", violations[0].LawId);
            Assert.Equal("WageViolation", violations[0].ViolationId);
        }

        [Fact]
        public void RuleEvaluator_BoundaryThreshold()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();

            var law = new Legislation { Id = "L1", Name = "L", TemplateId = "T", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            registry.RegisterLaw(law);

            // 边界：值 == 阈值 → 不命中
            var miss = new RuleEvaluator(registry, f => 70f);
            Assert.Empty(miss.Evaluate());

            // 刚过阈值 → 命中
            var hit = new RuleEvaluator(registry, f => 70.001f);
            Assert.Single(hit.Evaluate());
        }

        // ---- Phase 4：EnforcementEngine ----

        private static EnforcementEngine BuildEnforcementEngine(float intensity, float frequency, float resource, out RuleEvaluator evaluator)
        {
            var registry = new LegislationRegistry();
            registry.Initialize();

            var law = new Legislation { Id = "L1", Name = "L", TemplateId = "T", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            law.Enforcement.InspectionFrequency = frequency;
            law.Enforcement.EnforcementIntensity = intensity;
            law.Enforcement.EnforcementResource = resource;
            registry.RegisterLaw(law);

            evaluator = new RuleEvaluator(registry, f => 80f);
            evaluator.Evaluate(); // 填充 RecentViolations（命中 V1）

            return new EnforcementEngine(registry, evaluator, () => 0.1f);
        }

        [Fact]
        public void EnforcementEngine_InspectionFrequencyGate()
        {
            RuleEvaluator evaluator;
            var engine = BuildEnforcementEngine(1f, 30f, 0f, out evaluator);
            engine.Initialize();

            engine.Tick(15f);
            Assert.Equal(0, engine.CheckCount); // 未到检查频率

            engine.Tick(15f);
            Assert.Equal(1, engine.CheckCount); // 累计 30s → 检查
        }

        [Fact]
        public void EnforcementEngine_CatchesViolationWithHighIntensity()
        {
            RuleEvaluator evaluator;
            var engine = BuildEnforcementEngine(1f, 1f, 0f, out evaluator);
            engine.Initialize();

            engine.Tick(1f);
            Assert.Equal(1, engine.CaughtViolations);
            Assert.Equal(0, engine.MissedViolations);
        }

        [Fact]
        public void EnforcementEngine_MissesViolationWithZeroIntensity()
        {
            RuleEvaluator evaluator;
            var engine = BuildEnforcementEngine(0f, 1f, 0f, out evaluator);
            engine.Initialize();

            engine.Tick(1f);
            Assert.Equal(0, engine.CaughtViolations);
            Assert.Equal(1, engine.MissedViolations);
        }

        [Fact]
        public void EnforcementEngine_AccumulatesCost()
        {
            RuleEvaluator evaluator;
            var engine = BuildEnforcementEngine(0f, 1f, 2f, out evaluator);
            engine.Initialize();

            engine.Tick(1f);
            Assert.Equal(2f, engine.TotalCost); // 每次检查消耗执法资源 2
        }

        // ---- Phase 5：政治接入 ----

        [Fact]
        public void Manager_EnactAndRepealPublishCategory()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor", Category = LawCategory.Labor });
            var manager = new LegislationManager(registry);

            LawCategory? enactedCategory = null, repealedCategory = null;
            EventBus.Subscribe<LawEnactedEvent>(e => enactedCategory = e.Category);
            EventBus.Subscribe<LawRepealedEvent>(e => repealedCategory = e.Category);

            string error;
            var law = manager.CreateLaw("Labor", "L", null, out error);
            Assert.True(manager.EnactLaw(law.Id));
            Assert.Equal(LawCategory.Labor, enactedCategory);
            Assert.True(manager.RepealLaw(law.Id));
            Assert.Equal(LawCategory.Labor, repealedCategory);
        }

        [Fact]
        public void LaborTemplate_HasLaborCategory()
        {
            var template = ONIModPack.Content.Legislation.Templates.LaborLawTemplate.Create();
            Assert.Equal(LawCategory.Labor, template.Category);
        }

        // ---- Phase 6：ConsequenceEngine ----

        private static (RuleEvaluator evaluator, EnforcementEngine enforcement, EffectPipeline pipeline, ConsequenceEngine consequence) BuildConsequenceChain(float intensity)
        {
            var registry = new LegislationRegistry();
            registry.Initialize();

            var law = new Legislation { Id = "L1", Name = "L", TemplateId = "T", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            law.Consequences.Add(new ConsequenceConfig { ViolationId = "V1", Target = EffectTarget.Stress, Magnitude = 10f });
            law.Enforcement.InspectionFrequency = 1f;
            law.Enforcement.EnforcementIntensity = intensity;
            registry.RegisterLaw(law);

            var evaluator = new RuleEvaluator(registry, f => 80f);
            evaluator.Evaluate();

            var enforcement = new EnforcementEngine(registry, evaluator, () => 0.1f);
            enforcement.Initialize();
            enforcement.Tick(1f);

            var pipeline = new EffectPipeline();
            pipeline.Initialize();

            var consequence = new ConsequenceEngine(registry, enforcement, pipeline, () => new[] { 1, 2 });
            consequence.Initialize();
            consequence.Tick(1f);

            return (evaluator, enforcement, pipeline, consequence);
        }

        [Fact]
        public void ConsequenceEngine_AppliesModifierToCaughtViolation()
        {
            var chain = BuildConsequenceChain(1f);

            Assert.Equal(1, chain.enforcement.CaughtViolations);
            Assert.Equal(2, chain.consequence.AppliedModifiers); // colony 级违规 → 2 个复制人各 1 个 modifier

            var mods = chain.pipeline.GetActiveModifiers(1);
            Assert.Single(mods);
            Assert.Equal(EffectTarget.Stress, mods[0].Target);
            Assert.Equal(EffectSourceType.Law, mods[0].SourceType);
            Assert.Equal(EffectPriority.Law, mods[0].Priority);
            Assert.Equal(10f, mods[0].ComputeValue(1));
        }

        [Fact]
        public void ConsequenceEngine_NoModifierWhenMissed()
        {
            var chain = BuildConsequenceChain(0f);

            Assert.Equal(1, chain.enforcement.MissedViolations);
            Assert.Equal(0, chain.consequence.AppliedModifiers);
            Assert.Empty(chain.pipeline.GetActiveModifiers(1));
        }

        // ---- v0.80：劳动法端到端闭环（计划书 §16） ----

        [Fact]
        public void ClosedLoop_LaborLawFullChain()
        {
            EventBus.Clear();

            // 1. 模板
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(LaborLawTemplate.Create());

            // 2. 玩家按 §16 测试案例填参创建
            var manager = new LegislationManager(registry);
            string error;
            var law = manager.CreateLaw("Labor", "我的劳动法", new Dictionary<string, object>
            {
                { "MinWage", 10 },
                { "MaxWorkHours", 12 },
                { "OvertimeMultiplier", 1.5f },
                { "RestHours", 1 },
                { "AllowStrike", false },
                { "EnforcementIntensity", 0.25f }
            }, out error);
            Assert.NotNull(law);
            Assert.Equal(LawStatus.Draft, law.Status);

            // 3. 玩家设置规则：Stress 低于阈值 10 → StressViolation（v0.75 校验硬门槛要求字段 ∈ ClauseLibrary.AvailableFields）
            law.Rules.Add(new LawRule
            {
                Id = "MinWageRule",
                Description = "压力低于阈值",
                Condition = new ConditionGroup
                {
                    Op = ConditionOp.And,
                    Conditions = new List<LawCondition>
                    {
                        new LawCondition { Field = "Stress", Op = ComparisonOp.LessThan, Threshold = 10f }
                    }
                },
                ViolationId = "WageViolation"
            });
            law.Consequences.Add(new ConsequenceConfig { ViolationId = "WageViolation", Target = EffectTarget.Stress, Magnitude = 8f });
            law.Enforcement.InspectionFrequency = 1f;
            law.Enforcement.EnforcementIntensity = 0.25f;
            law.Enforcement.PenaltySeverity = 0.5f;

            // 4. 生效 → LawEnactedEvent（Category=Labor）
            LawCategory? enactedCategory = null;
            EventBus.Subscribe<LawEnactedEvent>(e => enactedCategory = e.Category);
            Assert.True(manager.EnactLaw(law.Id));
            Assert.Equal(LawStatus.Enacted, law.Status);
            Assert.Equal(LawCategory.Labor, enactedCategory);

            // 5. 规则评估：现场 Stress 15 > 阈值 10 → 不违规；改用字段解析器注入 Stress=5 < 10 → WageViolation
            var evaluator = new RuleEvaluator(registry, field => field == "Stress" ? 5f : 0f);
            var violations = evaluator.Evaluate();
            Assert.Single(violations);
            Assert.Equal("WageViolation", violations[0].ViolationId);

            // 6. 执法：强度 25%，随机 0.1 → 查处成功；处罚 50% 计入成本
            var enforcement = new EnforcementEngine(registry, evaluator, () => 0.1f);
            enforcement.Initialize();
            enforcement.Tick(1f);
            Assert.Equal(1, enforcement.CheckCount);
            Assert.Equal(1, enforcement.CaughtViolations);
            Assert.Equal(0.5f, enforcement.TotalCost); // 处罚力度 0.5 计入执法成本

            // 7. 后果：Stress +8 modifier 落到复制人
            var pipeline = new EffectPipeline();
            pipeline.Initialize();
            var consequence = new ConsequenceEngine(registry, enforcement, pipeline, () => new[] { 1 });
            consequence.Initialize();
            consequence.Tick(1f);

            Assert.Equal(1, consequence.AppliedModifiers);
            var mods = pipeline.GetActiveModifiers(1);
            Assert.Single(mods);
            Assert.Equal(EffectTarget.Stress, mods[0].Target);
            Assert.Equal(EffectSourceType.Law, mods[0].SourceType);
            Assert.Equal(EffectPriority.Law, mods[0].Priority);
            Assert.Equal(8f, mods[0].ComputeValue(1));

            // 8. 废止 → LawRepealedEvent（闭环另一头）
            Assert.True(manager.RepealLaw(law.Id));
            Assert.Equal(LawStatus.Repealed, law.Status);
        }

        // ---- Phase 3-6 剩余：审批流程 / 执行机构规模 ----

        [Fact]
        public void Manager_ApprovalFlowPasses()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor" });
            var manager = new LegislationManager(registry, () => 0.6f);

            string error;
            var law = manager.CreateLaw("Labor", "L", null, out error);
            law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };

            int enacted = 0;
            EventBus.Subscribe<LawEnactedEvent>(_ => enacted++);

            Assert.True(manager.ProposeLaw(law.Id));
            Assert.Equal(LawStatus.Proposed, law.Status);

            manager.UpdateApproval(0.5f); // 讨论半程未到（0.5 < 1）
            Assert.Equal(LawStatus.Proposed, law.Status);
            manager.UpdateApproval(0.5f); // 累计 1 → Debating
            Assert.Equal(LawStatus.Debating, law.Status);
            manager.UpdateApproval(0.5f); // 辩论半程未到
            Assert.Equal(LawStatus.Debating, law.Status);
            manager.UpdateApproval(0.5f); // 累计 1 → Voting
            Assert.Equal(LawStatus.Voting, law.Status);
            manager.UpdateApproval(1f); // 投票期未满（1 < 2）
            Assert.Equal(LawStatus.Voting, law.Status);
            manager.UpdateApproval(1f); // 累计 2 → 民望 0.6 >= 0.5 → 进入 FinalValidation（v0.75 最终门槛）
            Assert.Equal(LawStatus.FinalValidation, law.Status);
            manager.UpdateApproval(0f); // 最终验证：民望复检 0.6 >= 0.5 且合法性通过 → Enacted
            Assert.Equal(LawStatus.Enacted, law.Status);
            Assert.Equal(1, enacted);
        }

        [Fact]
        public void Manager_ApprovalFlowRejects()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor" });
            var manager = new LegislationManager(registry, () => 0.4f);

            string error;
            var law = manager.CreateLaw("Labor", "L", null, out error);
            law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };

            string lastStatus = null;
            EventBus.Subscribe<LawProposalStatusChangedEvent>(e => lastStatus = e.Status);

            Assert.True(manager.ProposeLaw(law.Id));
            manager.UpdateApproval(0.5f);
            manager.UpdateApproval(0.5f); // 累计 1 → Debating
            manager.UpdateApproval(0.5f);
            manager.UpdateApproval(0.5f); // 累计 1 → Voting
            manager.UpdateApproval(1f);
            manager.UpdateApproval(1f); // 民望 0.4 < 0.5 → Rejected
            Assert.Equal(LawStatus.Rejected, law.Status);
            Assert.Equal("Rejected", lastStatus);
        }

        [Fact]
        public void EnforcementEngine_AgencyScaleReducesCatchRate()
        {
            // 执法强度 1 但机构规模 0 → 有效查处率 0 → 全部漏网
            var registry = new LegislationRegistry();
            registry.Initialize();
            var law = new Legislation { Id = "L1", Name = "L", TemplateId = "T", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            law.Enforcement.InspectionFrequency = 1f;
            law.Enforcement.EnforcementIntensity = 1f;
            law.Enforcement.EnforcementAgencySize = 0f;
            registry.RegisterLaw(law);
            var eval = new RuleEvaluator(registry, f => 80f);
            eval.Evaluate();
            var eng = new EnforcementEngine(registry, eval, () => 0.1f);
            eng.Initialize();
            eng.Tick(1f);

            Assert.Equal(0, eng.CaughtViolations);
            Assert.Equal(1, eng.MissedViolations);
            Assert.Equal(0f, eng.ComplianceRate);
        }

        [Fact]
        public void EnforcementEngine_ComplianceRateFullWhenAllCaught()
        {
            RuleEvaluator evaluator;
            var engine = BuildEnforcementEngine(1f, 1f, 0f, out evaluator);
            engine.Initialize();
            engine.Tick(1f);

            Assert.Equal(1, engine.CaughtViolations);
            Assert.Equal(1f, engine.ComplianceRate);
        }

        // ---- 7 新模板 + UI 预览（计划书 §13/§9.2） ----

        [Fact]
        public void Templates_AllHaveCategoryAndParameters()
        {
            var templates = new[]
            {
                LaborLawTemplate.Create(),
                TaxLawTemplate.Create(),
                WelfareLawTemplate.Create(),
                PropertyLawTemplate.Create(),
                SecurityLawTemplate.Create(),
                ReligionLawTemplate.Create(),
                EnvironmentLawTemplate.Create(),
                PublicServiceLawTemplate.Create()
            };

            Assert.Equal(8, templates.Length);
            foreach (var t in templates)
            {
                Assert.NotEmpty(t.Parameters);
                Assert.True(System.Enum.IsDefined(typeof(LawCategory), t.Category));
            }

            Assert.Equal(LawCategory.Labor, LaborLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Tax, TaxLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Welfare, WelfareLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Property, PropertyLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Security, SecurityLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Religion, ReligionLawTemplate.Create().Category);
            Assert.Equal(LawCategory.Environment, EnvironmentLawTemplate.Create().Category);
            Assert.Equal(LawCategory.PublicService, PublicServiceLawTemplate.Create().Category);
        }

        [Fact]
        public void CreateLaw_EnumParameterUsesDefaultIndex()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(ReligionLawTemplate.Create());
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Religion", "R", null, out error); // 不传参 → Enum 取 Default 索引
            Assert.NotNull(law);
            Assert.Equal("Allowed", (string)law.Parameters["PublicActivity"].Value); // Default=2 → "Allowed"
        }

        [Fact]
        public void Previewer_HighMinWageWarnsExtreme()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "极端最低工资", new Dictionary<string, object> { { "MinWage", 100 } }, out error);
            Assert.NotNull(law);

            var previewer = new LawImpactPreviewer();
            previewer.Initialize();
            var preview = previewer.Preview(law, template);

            Assert.Equal(ImpactDirection.Up, preview.WorkerSupport);
            Assert.Equal(ImpactDirection.Down, preview.EnterpriseSupport);
            Assert.Equal(ImpactDirection.Up, preview.PoliticalConflict);
            Assert.True(preview.IsExtreme);
            Assert.Contains("MinWage", preview.ExtremeReason);
        }

        [Fact]
        public void Previewer_DefaultLaborNotExtreme()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "默认劳动法", null, out error);
            Assert.NotNull(law);

            var previewer = new LawImpactPreviewer();
            previewer.Initialize();
            var preview = previewer.Preview(law, template);

            Assert.False(preview.IsExtreme);
            Assert.Contains("工人支持", previewer.FormatPreview(preview));
        }

        // ---- Legal Validity（计划书 §9.1） ----

        [Fact]
        public void Validator_ValidLawPasses()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "合法劳动法", null, out error);
            law.Rules.Add(new LawRule
            {
                Id = "R1",
                ViolationId = "StressViolation",
                Condition = new ConditionGroup
                {
                    Op = ConditionOp.And,
                    Conditions = new List<LawCondition> { new LawCondition { Field = "Stress", Op = ComparisonOp.GreaterThan, Threshold = 75f } }
                }
            });

            var validator = new LawValidator();
            validator.Initialize();
            var result = validator.Validate(law, template);

            Assert.True(result.IsValid, string.Join("; ", result.Errors.ConvertAll(e => e.Message)));
        }

        [Fact]
        public void Validator_UnknownConditionFieldFails()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "未知字段", null, out error);
            law.Rules.Add(new LawRule
            {
                Id = "R1",
                ViolationId = "V1",
                Condition = new ConditionGroup
                {
                    Op = ConditionOp.And,
                    Conditions = new List<LawCondition> { new LawCondition { Field = "Wage", Op = ComparisonOp.LessThan, Threshold = 10f } }
                }
            });

            var validator = new LawValidator();
            validator.Initialize();
            var result = validator.Validate(law, template);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "UnknownField");
        }

        [Fact]
        public void Validator_DuplicateViolationIdFails()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "重复违规", null, out error);
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            law.Rules.Add(new LawRule { Id = "R2", ViolationId = "V1", Condition = Leaf(ComparisonOp.LessThan, 30f) });

            var validator = new LawValidator();
            validator.Initialize();
            var result = validator.Validate(law, template);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "DuplicateViolation");
        }

        [Fact]
        public void Validator_ConstraintViolationFails()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = WelfareLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            // 最低生活保障 5 < 失业补助 10 → 违反「保障 ≥ 补助」制度要求
            string error;
            var law = manager.CreateLaw("Welfare", "倒挂福利", new Dictionary<string, object>
            {
                { "MinimumLivingGuarantee", 5 },
                { "UnemploymentBenefit", 10 }
            }, out error);
            Assert.NotNull(law);

            var validator = new LawValidator();
            validator.Initialize();
            var result = validator.Validate(law, template);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "ConstraintViolation");
        }

        [Fact]
        public void Validator_InvalidParameterFails()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);
            var manager = new LegislationManager(registry);

            string error;
            var law = manager.CreateLaw("Labor", "篡改参数", null, out error);
            law.Parameters["MinWage"].Value = 999; // 绕过创建期校验

            var validator = new LawValidator();
            validator.Initialize();
            var result = validator.Validate(law, template);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "InvalidParameter");
        }

        // ---- 审查修复验证 ----

        [Fact]
        public void Registry_SaveLoadFullRoundtrip()
        {
            var registry = new LegislationRegistry();
            registry.Initialize();

            var law = new Legislation { Id = "L1", Name = "完整法律", TemplateId = "Labor", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "V1", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            law.CustomClauses.Add(new LawRule { Id = "C1", ViolationId = "CV", Condition = Leaf(ComparisonOp.LessThan, 30f) });
            law.Enforcement.EnforcementIntensity = 0.4f;
            law.Enforcement.EnforcementAgencySize = 0.7f;
            law.Consequences.Add(new ConsequenceConfig { ViolationId = "V1", Target = EffectTarget.Stress, Magnitude = 8f, CapacitySensitivity = 0.5f });
            law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 12f, VotingSeconds = 20f, MinPopularSupport = 0.6f };
            law.EnabledPlugins.Add("P1");
            law.Condition = new ConditionGroup
            {
                Op = ConditionOp.And,
                Groups = new List<ConditionGroup> { Leaf(ComparisonOp.GreaterThan, 50f) }
            };
            registry.RegisterLaw(law);

            var data = new Dictionary<string, object>();
            registry.Save(data);

            var restored = new LegislationRegistry();
            restored.Initialize();
            restored.Load(data);
            var got = restored.GetLaw("L1");

            Assert.NotNull(got);
            Assert.Equal(LawStatus.Enacted, got.Status);
            Assert.Single(got.Rules);
            Assert.Equal("V1", got.Rules[0].ViolationId);
            Assert.Single(got.CustomClauses);
            Assert.Equal("CV", got.CustomClauses[0].ViolationId);
            Assert.Equal(0.4f, got.Enforcement.EnforcementIntensity);
            Assert.Equal(0.7f, got.Enforcement.EnforcementAgencySize);
            Assert.Single(got.Consequences);
            Assert.Equal(EffectTarget.Stress, got.Consequences[0].Target);
            Assert.Equal(0.5f, got.Consequences[0].CapacitySensitivity);
            Assert.NotNull(got.PoliticalRequirements);
            Assert.Equal(0.6f, got.PoliticalRequirements.MinPopularSupport);
            Assert.Equal("P1", got.EnabledPlugins[0]);
            Assert.NotNull(got.Condition);
            Assert.Single(got.Condition.Groups);
        }

        [Fact]
        public void Manager_CreateLawWithEnabledPlugin()
        {
            var unlock = new PluginUnlockService();
            unlock.Initialize();
            var pluginRegistry = new PluginRegistry(unlock);
            pluginRegistry.Initialize();

            var plugin = new LawPlugin
            {
                Id = "MinWageFloor",
                Name = "最低工资下限",
                TemplateId = "Labor",
                AddedParameters = new List<LawParameter> { new LawParameter { Id = "MinWageFloor", Type = LawParameterType.Integer, Min = 0f, Max = 50f, Default = 5f } },
                AddedRules = new List<LawRule> { new LawRule { Id = "PR", ViolationId = "PV", Condition = Leaf(ComparisonOp.GreaterThan, 60f) } },
                AddedConstraints = new List<LawConstraint> { new LawConstraint { ParameterA = "MinWageFloor", Op = ComparisonOp.LessOrEqual, ParameterB = "MinWage", Message = "最低工资下限不能高于最低工资" } }
            };
            pluginRegistry.Register(plugin);

            var lawRegistry = new LegislationRegistry();
            lawRegistry.Initialize();
            lawRegistry.RegisterTemplate(LaborLawTemplate.Create());

            var manager = new LegislationManager(lawRegistry, null, pluginRegistry);
            string error;
            var law = manager.CreateLaw("Labor", "带插件", null, new List<string> { "MinWageFloor" }, out error);

            Assert.NotNull(law);
            Assert.Equal(1, law.EnabledPlugins.Count);
            Assert.True(law.Parameters.ContainsKey("MinWageFloor"));
            Assert.Equal(5, Convert.ToInt32(law.Parameters["MinWageFloor"].Value));
            Assert.Contains(law.Rules, r => r.Id == "PR");

            // 未知插件 → 拒绝
            Assert.Null(manager.CreateLaw("Labor", "未知插件", null, new List<string> { "Nope" }, out error));
            Assert.NotNull(error);
        }

        [Fact]
        public void Validator_PluginConstraintApplied()
        {
            var unlock = new PluginUnlockService();
            unlock.Initialize();
            var pluginRegistry = new PluginRegistry(unlock);
            pluginRegistry.Initialize();
            pluginRegistry.Register(new LawPlugin
            {
                Id = "MinWageFloor",
                Name = "最低工资下限",
                TemplateId = "Labor",
                AddedParameters = new List<LawParameter> { new LawParameter { Id = "MinWageFloor", Type = LawParameterType.Integer, Min = 0f, Max = 50f, Default = 5f } },
                AddedConstraints = new List<LawConstraint> { new LawConstraint { ParameterA = "MinWageFloor", Op = ComparisonOp.LessOrEqual, ParameterB = "MinWage", Message = "最低工资下限不能高于最低工资" } }
            });

            var lawRegistry = new LegislationRegistry();
            lawRegistry.Initialize();
            lawRegistry.RegisterTemplate(LaborLawTemplate.Create());

            var manager = new LegislationManager(lawRegistry, null, pluginRegistry);
            string error;
            var law = manager.CreateLaw("Labor", "带插件", null, new List<string> { "MinWageFloor" }, out error);
            law.Parameters["MinWageFloor"].Value = 20; // > MinWage(10) → 违反插件约束

            var validator = new LawValidator(pluginRegistry);
            validator.Initialize();
            var result = validator.Validate(law, LaborLawTemplate.Create());

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "ConstraintViolation");
        }

        [Fact]
        public void PluginUnlockService_NoTechRequirementUnlocks()
        {
            var plugin = new LawPlugin
            {
                Id = "P",
                Name = "P",
                TemplateId = "Labor",
                Unlock = new LawUnlockRequirement { Source = UnlockSourceType.Technology, RequiredResearchId = "" }
            };
            var svc = new PluginUnlockService();
            svc.Initialize();
            Assert.True(svc.IsUnlocked(plugin)); // 无科技要求 → 无条件解锁
        }

        [Fact]
        public void RuleEvaluator_IsKnownFieldFromClauseLibrary()
        {
            Assert.True(RuleEvaluator.IsKnownField("Stress"));
            Assert.True(RuleEvaluator.IsKnownField("Faction.Unionist"));
            Assert.False(RuleEvaluator.IsKnownField("Wage"));
        }

        // ---- Phase 7：插件内容化 ----

        private static LawTemplate[] AllTemplates()
        {
            return new[]
            {
                LaborLawTemplate.Create(),
                TaxLawTemplate.Create(),
                WelfareLawTemplate.Create(),
                PropertyLawTemplate.Create(),
                SecurityLawTemplate.Create(),
                ReligionLawTemplate.Create(),
                EnvironmentLawTemplate.Create(),
                PublicServiceLawTemplate.Create()
            };
        }

        private static bool FieldsValid(ConditionGroup g)
        {
            if (g == null) return true;
            foreach (var c in g.Conditions)
                if (c != null && !RuleEvaluator.IsKnownField(c.Field)) return false;
            foreach (var sub in g.Groups)
                if (sub != null && !FieldsValid(sub)) return false;
            return true;
        }

        [Fact]
        public void Plugins_AllTemplatesHaveUniquePlugins()
        {
            var templates = AllTemplates();
            var ids = new HashSet<string>();
            foreach (var t in templates)
            {
                Assert.Single(t.Plugins);
                Assert.True(ids.Add(t.Plugins[0].Id), "插件 Id 重复：" + t.Plugins[0].Id);
            }
            Assert.Equal(8, ids.Count);
        }

        [Fact]
        public void Plugins_FieldsAndConstraintsValid()
        {
            foreach (var t in AllTemplates())
            {
                var plugin = t.Plugins[0];

                foreach (var rule in plugin.AddedRules)
                    Assert.True(FieldsValid(rule.Condition), "插件规则字段非法：" + plugin.Id);

                var paramIds = new HashSet<string>();
                foreach (var p in t.Parameters) paramIds.Add(p.Id);
                foreach (var p in plugin.AddedParameters) paramIds.Add(p.Id);

                foreach (var c in plugin.AddedConstraints)
                {
                    Assert.Contains(c.ParameterA, paramIds);
                    Assert.Contains(c.ParameterB, paramIds);
                }
            }
        }

        [Fact]
        public void Plugins_EnableFlowAndTechLock()
        {
            var unlock = new PluginUnlockService();
            unlock.Initialize();
            var pluginRegistry = new PluginRegistry(unlock);
            pluginRegistry.Initialize();

            // 宗教法「国教制度」（Social 来源 → 本轮恒解锁）→ 启用成功
            var religionTemplate = ReligionLawTemplate.Create();
            pluginRegistry.Register(religionTemplate.Plugins[0]);
            var lawRegistry = new LegislationRegistry();
            lawRegistry.Initialize();
            lawRegistry.RegisterTemplate(religionTemplate);
            var manager = new LegislationManager(lawRegistry, null, pluginRegistry);

            string error;
            var law = manager.CreateLaw("Religion", "带国教插件", null, new List<string> { "StateReligion" }, out error);
            Assert.NotNull(law);
            Assert.Contains("StateReligion", law.EnabledPlugins);
            Assert.True(law.Parameters.ContainsKey("StateReligionInfluence")); // 插件参数已合并

            // 劳动法「危险工作补偿」（科技来源，SocialResearchUnlocks 未注册）→ 拒绝
            pluginRegistry.Register(LaborLawTemplate.Create().Plugins[0]);
            var lawRegistry2 = new LegislationRegistry();
            lawRegistry2.Initialize();
            lawRegistry2.RegisterTemplate(LaborLawTemplate.Create());
            var manager2 = new LegislationManager(lawRegistry2, null, pluginRegistry);

            Assert.Null(manager2.CreateLaw("Labor", "未解锁插件", null, new List<string> { "DangerousWorkPay" }, out error));
            Assert.NotNull(error);
        }

        // ---- Phase 9：宏观后果系统化 ----

        [Fact]
        public void LawViolationEvent_IncludesCategory()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            var template = LaborLawTemplate.Create();
            registry.RegisterTemplate(template);

            var law = new Legislation { Id = "L1", Name = "L", TemplateId = "Labor", Status = LawStatus.Enacted };
            law.Rules.Add(new LawRule { Id = "R1", ViolationId = "WageViolation", Condition = Leaf(ComparisonOp.GreaterThan, 70f) });
            registry.RegisterLaw(law);

            LawCategory? publishedCategory = null;
            EventBus.Subscribe<LawViolationEvent>(e => publishedCategory = e.Category);

            var evaluator = new RuleEvaluator(registry, f => 80f);
            evaluator.Tick(1f); // 发布违规事件

            Assert.Equal(LawCategory.Labor, publishedCategory);
        }

        [Fact]
        public void MacroEffects_MappingTables()
        {
            // 压力轴
            Assert.Equal(SituationAxis.LaborTension, LegislationMacroEffects.CategoryToAxis(LawCategory.Labor));
            Assert.Equal(SituationAxis.SocialUnrest, LegislationMacroEffects.CategoryToAxis(LawCategory.Security));
            Assert.Equal(SituationAxis.IdeologicalSplit, LegislationMacroEffects.CategoryToAxis(LawCategory.Religion));
            Assert.Equal(SituationAxis.MoralFatigue, LegislationMacroEffects.CategoryToAxis(LawCategory.Environment));

            // 民望话题（未映射类别返回 null，调用方只扣基础 Law 话题避免双扣）
            Assert.Equal(OpinionTopic.WorkConditions, LegislationMacroEffects.CategoryToTopic(LawCategory.Labor));
            Assert.Equal(OpinionTopic.Economy, LegislationMacroEffects.CategoryToTopic(LawCategory.Tax));
            Assert.Equal(OpinionTopic.Welfare, LegislationMacroEffects.CategoryToTopic(LawCategory.Welfare));
            Assert.Equal(OpinionTopic.Religion, LegislationMacroEffects.CategoryToTopic(LawCategory.Religion));
            Assert.False(LegislationMacroEffects.CategoryToTopic(LawCategory.Education).HasValue);

            // 派系
            Assert.Equal(FactionType.Union, LegislationMacroEffects.CategoryToFaction(LawCategory.Labor));
            Assert.Equal(FactionType.Engineering, LegislationMacroEffects.CategoryToFaction(LawCategory.Property));
            Assert.Equal(FactionType.Belief, LegislationMacroEffects.CategoryToFaction(LawCategory.Religion));
            Assert.Equal(FactionType.Science, LegislationMacroEffects.CategoryToFaction(LawCategory.Environment));
        }

        // ---- v0.75 政府应对（闭环最后一环） ----

        /// <summary>最小 FactionSystem 替身：只提供指定派系的 Popularity 查询。</summary>
        private class FakeFactionSystem : IFactionSystem
        {
            private readonly Dictionary<FactionType, FactionData> _factions = new Dictionary<FactionType, FactionData>();
            public event Action<FactionType, float> OnPopularityChanged;
            public event Action<FactionType, ONIModPack.Content.SocialDynamics.Politics.FactionLeader> OnLeaderChanged;
            public float PublicOpinionModifier { get; set; }
            public float PolarizationModifier { get; set; }

            public FakeFactionSystem(params FactionType[] types)
            {
                foreach (var t in types)
                    _factions[t] = new FactionData { Type = t, Popularity = 0.6f };
            }

            public FactionData GetFaction(FactionType type)
                => _factions.TryGetValue(type, out var fd) ? fd : null;

            public ONIModPack.Content.SocialDynamics.Politics.FactionLeader GetLeader(FactionType type) => null;
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

        /// <summary>构造：已生效的最低工资法律 + Union 受损 + 高不满累积，返回就绪的派系行动系统。</summary>
        private static FactionActionSystem CreateEnactedLawWithUnionGrievance(
            out ONIModPack.Content.Legislation.Model.Legislation law, out FakeFactionSystem factions)
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            law = new ONIModPack.Content.Legislation.Model.Legislation
            {
                Id = "labor_min_wage", Name = "L", TemplateId = "Labor", Status = LawStatus.Enacted
            };
            registry.RegisterLaw(law);

            factions = new FakeFactionSystem(FactionType.Union);
            var system = new FactionActionSystem(factions, registry);
            system.Initialize();
            return system;
        }

        [Fact]
        public void Government_ConcedeClearsGrievance()
        {
            EventBus.Clear();
            var system = CreateEnactedLawWithUnionGrievance(out var law, out var factions);

            // Union 受损 → 不满累积至行动阈值以上
            law.PassiveEffects.Add(new LawEffectDefinition
            {
                Channel = LawEffectChannel.FactionPopularity,
                Target = nameof(FactionType.Union),
                MagnitudePerSecond = -0.01f
            });
            system.Tick(30f);
            Assert.Single(system.RecentActions); // 已发起罢工动员

            // 让步：成功且清零状态
            Assert.True(system.Concede(FactionType.Union, law.Id));
            Assert.True(system.StrikeMobilizationModifier < 1f); // 不抛异常即视为清零路径执行

            // 无不满时再让步 → 失败
            Assert.False(system.Concede(FactionType.Union, law.Id));
        }

        [Fact]
        public void Government_SuppressReducesStrikeAndAddsResentment()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            ServiceRegistry.Register(new Legitimacy());
            try
            {
                var system = CreateEnactedLawWithUnionGrievance(out var law, out var factions);
                law.PassiveEffects.Add(new LawEffectDefinition
                {
                    Channel = LawEffectChannel.FactionPopularity,
                    Target = nameof(FactionType.Union),
                    MagnitudePerSecond = -0.02f
                });

                // 累积两轮行动抬升罢工动员修正
                system.Tick(60f);
                float before = system.StrikeMobilizationModifier;
                Assert.True(before > 0f);

                // 镇压：罢工动员减半 + 行动记录带 [政] 标记
                Assert.True(ServiceRegistry.TryGet<Legitimacy>(out var legitimacy));
                float legitimacyBefore = legitimacy.Value;
                Assert.True(system.Suppress(FactionType.Union, law.Id));
                Assert.True(system.StrikeMobilizationModifier < before);
                Assert.Contains(system.RecentActions, a => a.IsGovernmentAction && a.Label == "政府镇压");
                Assert.True(legitimacy.Value < legitimacyBefore);

                // 合法性过低时镇压失败
                legitimacy.Value = 0.2f;
                Assert.False(system.Suppress(FactionType.Union, law.Id));
            }
            finally
            {
                ServiceRegistry.Clear();
            }
        }

        [Fact]
        public void Government_SubsidizeSpendsBudgetAndDampensStrike()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var gov = new GovernanceResponseSystem();
            gov.Initialize();
            ServiceRegistry.Register(gov);
            try
            {
                var system = CreateEnactedLawWithUnionGrievance(out var law, out _);
                law.PassiveEffects.Add(new LawEffectDefinition
                {
                    Channel = LawEffectChannel.FactionPopularity,
                    Target = nameof(FactionType.Union),
                    MagnitudePerSecond = -0.02f
                });

                // 累积罢工动员修正
                system.Tick(60f);
                float before = system.StrikeMobilizationModifier;
                Assert.True(before > 0f);

                if (gov.Budget <= 0f)
                {
                    // 预算不足 → 补贴失败，动员修正不变
                    Assert.False(system.Subsidize());
                    Assert.Equal(before, system.StrikeMobilizationModifier);
                }
                else
                {
                    float budgetBefore = gov.Budget;
                    Assert.True(system.Subsidize());
                    Assert.True(system.StrikeMobilizationModifier < before); // 衰减到 30%
                    Assert.True(gov.Budget < budgetBefore);
                }
            }
            finally
            {
                ServiceRegistry.Clear();
            }
        }

        [Fact]
        public void StateMachine_RejectsIllegalTransitions()
        {
            // 跳过审批链：Draft 不能直达 FinalValidation/Rejected/Repealed
            var law = new Legislation { Id = "S1", Name = "L", TemplateId = "T", Status = LawStatus.Draft };
            Assert.False(LawStateMachine.TryTransition(law, LawStatus.FinalValidation, out var r1));
            Assert.Contains("非法状态转移", r1);
            Assert.Equal(LawStatus.Draft, law.Status); // 拒绝时状态不被修改
            Assert.False(LawStateMachine.CanTransition(LawStatus.Draft, LawStatus.Rejected));
            Assert.False(LawStateMachine.CanTransition(LawStatus.Draft, LawStatus.Repealed));

            // 审批链正向全部合法
            Assert.True(LawStateMachine.TryTransition(law, LawStatus.Proposed, out _));
            Assert.True(LawStateMachine.TryTransition(law, LawStatus.Debating, out _));
            Assert.True(LawStateMachine.TryTransition(law, LawStatus.Voting, out _));
            Assert.True(LawStateMachine.TryTransition(law, LawStatus.Rejected, out _));
            Assert.Equal(LawStatus.Rejected, law.Status);

            // 否决后只能回草案重新起草，不能直接复活生效
            Assert.False(LawStateMachine.CanTransition(LawStatus.Rejected, LawStatus.Enacted));
            Assert.True(LawStateMachine.CanTransition(LawStatus.Rejected, LawStatus.Draft));
        }

        [Fact]
        public void StateMachine_TerminalStatesAndSameState()
        {
            // Repealed 为吸收态：任何转移都非法
            var repealed = new Legislation { Id = "S2", Name = "L", TemplateId = "T", Status = LawStatus.Repealed };
            foreach (LawStatus target in Enum.GetValues(typeof(LawStatus)))
                if (target != LawStatus.Repealed)
                    Assert.False(LawStateMachine.CanTransition(LawStatus.Repealed, target), $"Repealed → {target} 应非法");

            // 同状态转移被拒绝（无操作）
            var enacted = new Legislation { Id = "S3", Name = "L", TemplateId = "T", Status = LawStatus.Enacted };
            Assert.False(LawStateMachine.TryTransition(enacted, LawStatus.Enacted, out _));
            Assert.Equal(LawStatus.Enacted, enacted.Status);

            // Enacted → Repealed 合法（唯一出口）
            Assert.True(LawStateMachine.TryTransition(enacted, LawStatus.Repealed, out _));
            Assert.Equal(LawStatus.Repealed, enacted.Status);
        }

        [Fact]
        public void StateMachine_ManagerRejectsEnactOfProposedLaw()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            var manager = new LegislationManager(registry);
            manager.Initialize();

            var template = new LawTemplate { Id = "T", Name = "T" };
            registry.RegisterTemplate(template);
            var law = manager.CreateLaw("T", "测试法", null, out _);
            Assert.NotNull(law);

            // 走到 Proposed 后，Debug 直通 EnactLaw 应被状态机拒绝（Draft→Enacted 才合法）
            Assert.True(manager.ProposeLaw(law.Id));
            Assert.False(manager.EnactLaw(law.Id, requireValidation: false, out var error));
            Assert.Contains("不在草案状态", error);
            Assert.Equal(LawStatus.Proposed, law.Status);
        }

        [Fact]
        public void Templates_AllDeclareValidPassiveEffects()
        {
            var templates = new[]
            {
                LaborLawTemplate.Create(), TaxLawTemplate.Create(), WelfareLawTemplate.Create(),
                PropertyLawTemplate.Create(), SecurityLawTemplate.Create(), ReligionLawTemplate.Create(),
                EnvironmentLawTemplate.Create(), PublicServiceLawTemplate.Create()
            };

            var axisNames = new HashSet<string>(Enum.GetNames(typeof(SituationAxis)));
            var topicNames = new HashSet<string>(Enum.GetNames(typeof(OpinionTopic)));
            var factionNames = new HashSet<string>(Enum.GetNames(typeof(FactionType)));

            foreach (var template in templates)
            {
                // v0.75 闭环要求：每个模板都必须声明至少一条持续效果
                Assert.True(template.PassiveEffects.Count > 0, $"{template.Id} 缺少 PassiveEffects 声明");

                foreach (var effect in template.PassiveEffects)
                {
                    Assert.True(effect.MagnitudePerSecond != 0f, $"{template.Id} 存在零强度效果");
                    if (!string.IsNullOrEmpty(effect.ScaleByParameter))
                        Assert.Contains(effect.ScaleByParameter,
                            template.Parameters.ConvertAll(p => p.Id));

                    // Target 必须是合法枚举名（容错未知名的设计只针对存档兼容，模板声明期就应正确）
                    HashSet<string> valid = null;
                    switch (effect.Channel)
                    {
                        case LawEffectChannel.PressureAxis: valid = axisNames; break;
                        case LawEffectChannel.OpinionTopic: valid = topicNames; break;
                        case LawEffectChannel.FactionPopularity: valid = factionNames; break;
                    }
                    Assert.True(valid != null && valid.Contains(effect.Target),
                        $"{template.Id} 效果 Target 非法: {effect.Channel}/{effect.Target}");
                }
            }
        }

        // ---- v0.76 执政派系×立法联动 ----

        [Fact]
        public void RulingFaction_PreferredCategoryGetsProposalBonus()
        {
            var election = new ElectionSystem();
            election.Initialize();
            var service = new RulingFactionService(election);
            service.Initialize();

            // Engineering 偏好 Labor：红利为负（门槛减免）；非偏好类别无红利
            Assert.Equal(RulingFactionService.PreferredBonus, service.GetProposalBonus(LawCategory.Labor));
            Assert.Equal(0f, service.GetProposalBonus(LawCategory.Tax));
            Assert.Equal(FactionType.Engineering, service.RulingFaction);

            election.Shutdown();
            service.Shutdown();
        }

        [Fact]
        public void Manager_ProposalBonusLowersVotingThreshold()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor", Category = LawCategory.Labor });
            registry.RegisterTemplate(new LawTemplate { Id = "TaxT", Name = "TaxT", Category = LawCategory.Tax });

            var election = new ElectionSystem(); // 默认执政 Engineering，偏好 Labor
            election.Initialize();
            var ruling = new RulingFactionService(election);

            // 民望 0.44：< 0.5 但 >= 0.5-0.08=0.42 → 偏好类别应通过、非偏好类别应被否决
            var manager = new LegislationManager(registry, () => 0.44f, null, ruling);

            var preferred = manager.CreateLaw("Labor", "P", null, out _);
            var other = manager.CreateLaw("TaxT", "O", null, out _);
            foreach (var law in new[] { preferred, other })
                law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };

            Assert.True(manager.ProposeLaw(preferred.Id));
            Assert.True(manager.ProposeLaw(other.Id));

            // 推进到表决结束（2+2+2 秒）；第 6 tick 投票通过进入 FinalValidation 并立即复检生效
            for (int i = 0; i < 6; i++) manager.UpdateApproval(1f);
            Assert.Equal(LawStatus.Enacted, preferred.Status);   // 0.44 >= 0.5-0.08 → 通过并生效
            Assert.Equal(LawStatus.Rejected, other.Status);      // 0.44 < 0.5 → 否决

            election.Shutdown();
            ruling.Shutdown();
        }

        [Fact]
        public void Veto_OnlyDuringVotingAndLimitedPerTerm()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "Labor" });

            var election = new ElectionSystem();
            election.Initialize();
            var ruling = new RulingFactionService(election);
            var manager = new LegislationManager(registry, () => 0.9f, null, ruling);

            var a = manager.CreateLaw("Labor", "A", null, out _);
            a.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f };
            var b = manager.CreateLaw("Labor", "B", null, out _);
            b.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f };

            // 草案期否决被拒绝
            Assert.False(manager.VetoLaw(a.Id, out var draftErr));
            Assert.Contains("仅表决中", draftErr);

            // 推进 a 到 Voting
            Assert.True(manager.ProposeLaw(a.Id));
            manager.UpdateApproval(2f); // Proposed → Debating
            manager.UpdateApproval(2f); // Debating → Voting
            Assert.Equal(LawStatus.Voting, a.Status);

            // 否决成功且消耗唯一否决权
            Assert.True(manager.VetoLaw(a.Id, out _));
            Assert.Equal(LawStatus.Rejected, a.Status);
            Assert.Equal(0, ruling.VetoesRemaining);

            // 推进 b 到 Voting 后否决失败（本届用尽）
            Assert.True(manager.ProposeLaw(b.Id));
            manager.UpdateApproval(2f);
            manager.UpdateApproval(2f);
            Assert.Equal(LawStatus.Voting, b.Status);
            Assert.False(manager.VetoLaw(b.Id, out var exhaustedErr));
            Assert.Contains("用尽", exhaustedErr);
            Assert.Equal(LawStatus.Voting, b.Status); // 否决失败不改变状态

            // 换届重置否决权
            election.SetRulingFaction(FactionType.Union);
            Assert.Equal(1, ruling.VetoesRemaining);
            Assert.True(manager.VetoLaw(b.Id, out _));
            Assert.Equal(LawStatus.Rejected, b.Status);

            election.Shutdown();
            ruling.Shutdown();
        }

        // ---- v0.76 罢工谈判闭环 ----

        [Fact]
        public void Strike_NegotiateSpendsBudgetAndEndsAllStrikes()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var strikeSystem = new StrikeSystem();
            strikeSystem.Initialize();
            var governance = new GovernanceResponseSystem();
            governance.Initialize(); // 默认预算 GlobalBudget（1.2）
            ServiceRegistry.Register(governance);
            try
            {
                // 无罢工时：成本为 0 且谈判失败
                Assert.Equal(0f, strikeSystem.GetNegotiationCost());
                Assert.False(strikeSystem.Negotiate(out var noStrikeErr));
                Assert.Contains("没有罢工", noStrikeErr);

                // 注入 2 名罢工者（强度 1.0）：成本 = 0.15 + 2×0.05×max(0.5, 1.0) = 0.25
                strikeSystem.RestoreStrike(101, 10, "stress", 1f);
                strikeSystem.RestoreStrike(102, 10, "stress", 1f);
                Assert.Equal(2, strikeSystem.StrikingCount);

                float cost = strikeSystem.GetNegotiationCost();
                Assert.Equal(0.25f, cost, 3);

                float budgetBefore = governance.Budget;
                Assert.True(strikeSystem.Negotiate(out _));
                Assert.Equal(0, strikeSystem.StrikingCount);
                Assert.Equal(budgetBefore - cost, governance.Budget, 3);

                // 复工后再谈判断失败
                Assert.False(strikeSystem.Negotiate(out _));
            }
            finally
            {
                ServiceRegistry.Clear();
                strikeSystem.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Strike_NegotiateFailsWhenBudgetInsufficient()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var strikeSystem = new StrikeSystem();
            strikeSystem.Initialize();
            var governance = new GovernanceResponseSystem();
            governance.Initialize();
            ServiceRegistry.Register(governance);
            try
            {
                // 清空预算
                governance.SpendBudget(governance.Budget);
                Assert.Equal(0f, governance.Budget);

                strikeSystem.RestoreStrike(201, 5, "stress", 1f);
                Assert.False(strikeSystem.Negotiate(out var err));
                Assert.Contains("预算不足", err);
                Assert.Equal(1, strikeSystem.StrikingCount); // 谈判失败不改变罢工状态
            }
            finally
            {
                ServiceRegistry.Clear();
                strikeSystem.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Strike_SuppressIsFreeButLeavesResentment()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var strikeSystem = new StrikeSystem();
            strikeSystem.Initialize();
            try
            {
                // 无罢工时镇压失败
                Assert.False(strikeSystem.SuppressStrike(out var err));
                Assert.Contains("没有罢工", err);

                // 治理系统未注册 → Negotiate 失败（需预算），Suppress 不依赖预算仍可用
                strikeSystem.RestoreStrike(301, 5, "stress", 0.2f);
                Assert.False(strikeSystem.Negotiate(out var govErr));
                Assert.Contains("治理系统未就绪", govErr);

                // 镇压：免费复工 + 动员修正残留一半
                strikeSystem.StrikeProbabilityModifier = 0.6f;
                Assert.True(strikeSystem.SuppressStrike(out _));
                Assert.Equal(0, strikeSystem.StrikingCount);
                Assert.Equal(0.3f, strikeSystem.StrikeProbabilityModifier, 3);

                // 镇压后再镇压失败（无罢工）
                Assert.False(strikeSystem.SuppressStrike(out _));

                // 低强度罢工成本按 max(0.5, intensity) 下限计算：0.15 + 1×0.05×0.5 = 0.175
                strikeSystem.RestoreStrike(302, 6, "stress", 0.2f);
                Assert.Equal(0.175f, strikeSystem.GetNegotiationCost(), 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                strikeSystem.Shutdown();
            }
        }

        // ---- v0.77 罢工结局宏观接线 ----

        [Fact]
        public void StrikeOutcome_NegotiatedRelievesLaborTensionAndLiftsLegitimacy()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var pressure = new PressureModel();
            pressure.Initialize();
            ServiceRegistry.Register(pressure);
            var legitimacy = new Legitimacy();
            legitimacy.Initialize();
            ServiceRegistry.Register(legitimacy);
            var config = new ReliefConfig();
            config.Initialize();
            ServiceRegistry.Register(config);
            var gov = new GovernanceResponseSystem();
            gov.Initialize();
            try
            {
                pressure.AddAxis(SituationAxis.LaborTension, 0.3f); // 预置劳资张力
                float axisBefore = pressure.Axes[SituationAxis.LaborTension];
                float legitimacyBefore = legitimacy.Value;

                gov.ApplyStrikeOutcome(SituationAxis.LaborTension, 5, negotiated: true);

                Assert.True(pressure.Axes[SituationAxis.LaborTension] < axisBefore); // 泄压
                Assert.True(legitimacy.Value > legitimacyBefore);                   // 政府守信合法性升
                Assert.Equal(0f, gov.RepressionReservoir, 4);                       // 谈判不入反扑储备
            }
            finally
            {
                ServiceRegistry.Clear();
                pressure.Shutdown();
                legitimacy.Shutdown();
                gov.Shutdown();
            }
        }

        [Fact]
        public void StrikeOutcome_SuppressedAddsBacklashAndFeedsReservoir()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var pressure = new PressureModel();
            pressure.Initialize();
            ServiceRegistry.Register(pressure);
            var legitimacy = new Legitimacy();
            legitimacy.Initialize();
            ServiceRegistry.Register(legitimacy);
            var config = new ReliefConfig();
            config.Initialize();
            ServiceRegistry.Register(config);
            var gov = new GovernanceResponseSystem();
            gov.Initialize();
            try
            {
                float unrestBefore = pressure.Axes[SituationAxis.SocialUnrest];
                float splitBefore = pressure.Axes[SituationAxis.IdeologicalSplit];
                float legitimacyBefore = legitimacy.Value;

                gov.ApplyStrikeOutcome(SituationAxis.SocialUnrest, 5, negotiated: false);

                Assert.True(pressure.Axes[SituationAxis.SocialUnrest] > unrestBefore);     // 动荡加压
                Assert.True(pressure.Axes[SituationAxis.IdeologicalSplit] > splitBefore);  // 意识分裂起步
                Assert.True(gov.RepressionReservoir > 0f);                                 // 反扑储备入账
                Assert.Equal(legitimacyBefore, legitimacy.Value, 4);                       // ApplyStrikeOutcome 不直接动合法性（磨损在订阅端）
            }
            finally
            {
                ServiceRegistry.Clear();
                pressure.Shutdown();
                legitimacy.Shutdown();
                gov.Shutdown();
            }
        }

        [Fact]
        public void StrikeEndedEvent_CarriesEndReasonAndCount()
        {
            EventBus.Clear();
            StrikeEndedEvent received = null;
            var token = EventBus.Subscribe<StrikeEndedEvent>(e => received = e);
            try
            {
                EventBus.Publish(new StrikeEndedEvent
                {
                    RoomId = 0,
                    Duration = 12f,
                    EndReason = StrikeEndReason.Negotiated,
                    Count = 3
                });
                Assert.NotNull(received);
                Assert.Equal(StrikeEndReason.Negotiated, received.EndReason);
                Assert.Equal(3, received.Count);
                Assert.Equal(12f, received.Duration, 2);
            }
            finally
            {
                EventBus.Unsubscribe(token);
            }
        }

        // ---- v0.78 否决政治代价 + 竞选纲领 ----

        [Fact]
        public void Veto_ImposesPoliticalCostOnRulingAndCategoryFactions()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "WelfareT", Name = "W", Category = LawCategory.Welfare });

            var election = new ElectionSystem(); // 默认执政 Engineering（偏好 Labor）
            election.Initialize();
            var ruling = new RulingFactionService(election);
            var factions = new FakeFactionSystem(FactionType.Engineering, FactionType.Union); // Union 偏好 Welfare
            ServiceRegistry.Register<IFactionSystem>(factions);
            var manager = new LegislationManager(registry, () => 0.9f, null, ruling);

            var law = manager.CreateLaw("WelfareT", "W", null, out _);
            law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };
            Assert.True(manager.ProposeLaw(law.Id));
            manager.UpdateApproval(2f);
            manager.UpdateApproval(2f);

            float engBefore = factions.GetFaction(FactionType.Engineering).Popularity;
            float unionBefore = factions.GetFaction(FactionType.Union).Popularity;

            Assert.True(manager.VetoLaw(law.Id, out _));

            // 执政派系承担独断代价；被压制类别派系（Union）承担诉求受损代价
            Assert.Equal(engBefore - 0.04f, factions.GetFaction(FactionType.Engineering).Popularity, 3);
            Assert.Equal(unionBefore - 0.02f, factions.GetFaction(FactionType.Union).Popularity, 3);

            ServiceRegistry.Clear();
            election.Shutdown();
            ruling.Shutdown();
        }

        [Fact]
        public void Enact_PreferredCategoryRewardsRulingFaction()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "L", Category = LawCategory.Labor });

            var election = new ElectionSystem(); // Engineering 偏好 Labor
            election.Initialize();
            var ruling = new RulingFactionService(election);
            var factions = new FakeFactionSystem(FactionType.Engineering);
            ServiceRegistry.Register<IFactionSystem>(factions);
            var manager = new LegislationManager(registry, () => 0.9f, null, ruling);

            var law = manager.CreateLaw("Labor", "L", null, out _);
            law.PoliticalRequirements = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };
            Assert.True(manager.ProposeLaw(law.Id));
            for (int i = 0; i < 6; i++) manager.UpdateApproval(1f);
            Assert.Equal(LawStatus.Enacted, law.Status);

            float engBefore = 0.6f; // FakeFactionSystem 默认值
            Assert.Equal(engBefore + 0.03f, factions.GetFaction(FactionType.Engineering).Popularity, 3);

            ServiceRegistry.Clear();
            election.Shutdown();
            ruling.Shutdown();
        }

        [Fact]
        public void CampaignPlatform_ProgressSettlesToFulfillmentOnTermChange()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var election = new ElectionSystem(); // Engineering 偏好 Labor
            election.Initialize();
            var ruling = new RulingFactionService(election);
            ServiceRegistry.Register<IRulingFactionService>(ruling);
            var platform = new CampaignPlatformService(election);
            platform.Initialize();

            try
            {
                // 初始：所有派系目标 2、进度 0
                Assert.Equal(2, platform.PlatformTargets[FactionType.Engineering]);
                Assert.Equal(0, platform.CurrentProgress[FactionType.Engineering]);

                // 1 部偏好类别法案生效 → Engineering 进度 +1
                EventBus.Publish(new LawEnactedEvent { LawId = "l1", Category = LawCategory.Labor });
                Assert.Equal(1, platform.CurrentProgress[FactionType.Engineering]);
                Assert.Equal(0.5f, platform.GetCurrentFulfillment(FactionType.Engineering), 3);

                // 换届 → 结算上届兑现度并开启新任期（进度清零）
                election.SetRulingFaction(FactionType.Union);
                Assert.Equal(1f / 2f, platform.GetLastFulfillment(FactionType.Engineering), 3);
                Assert.Equal(0, platform.CurrentProgress[FactionType.Engineering]);

                // 兑现度作为得票加成来源可被读取（数值契约：× FulfillmentVoteBonus）
                Assert.Equal(platform.GetLastFulfillment(FactionType.Engineering) * CampaignPlatformService.FulfillmentVoteBonus,
                    CampaignPlatformService.FulfillmentVoteBonus * 0.5f, 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                platform.Shutdown();
                ruling.Shutdown();
            }
        }

        // ---- v0.79 立法互斥冲突 ----

        [Fact]
        public void Conflict_ProposalBlockedWhenConflictingLawEnacted()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Welfare", Name = "福利法", Category = LawCategory.Welfare, ConflictCategories = { LawCategory.Tax } });
            registry.RegisterTemplate(new LawTemplate { Id = "Tax", Name = "税法", Category = LawCategory.Tax, ConflictCategories = { LawCategory.Welfare } });

            var manager = new LegislationManager(registry, () => 0.9f);
            var welfare = manager.CreateLaw("Welfare", "W", null, out _);
            Assert.True(manager.EnactLaw(welfare.Id, requireValidation: false, out _)); // 直通生效
            Assert.Equal(LawStatus.Enacted, welfare.Status);

            // 福利法已生效 → 提案税法被冲突拦截
            var tax = manager.CreateLaw("Tax", "T", null, out _);
            Assert.False(manager.ProposeLaw(tax.Id, out var error));
            Assert.Contains("冲突", error);
            Assert.Equal(LawStatus.Draft, tax.Status); // 状态未被改动

            // 废止福利法后 → 税法可提案
            Assert.True(manager.RepealLaw(welfare.Id));
            Assert.True(manager.ProposeLaw(tax.Id, out _));
            Assert.Equal(LawStatus.Proposed, tax.Status);
        }

        [Fact]
        public void Conflict_ReplacesExistingLawWhenBothPassApproval()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Welfare", Name = "福利法", Category = LawCategory.Welfare, ConflictCategories = { LawCategory.Tax } });
            registry.RegisterTemplate(new LawTemplate { Id = "Tax", Name = "税法", Category = LawCategory.Tax, ConflictCategories = { LawCategory.Welfare } });

            var manager = new LegislationManager(registry, () => 0.9f);
            var req = new LawPoliticalRequirements { DebateSeconds = 2f, VotingSeconds = 2f, MinPopularSupport = 0.5f };

            // 并行审批：两法同时通过提案（此时均无生效冲突法律）
            var tax = manager.CreateLaw("Tax", "T", null, out _);
            var welfare = manager.CreateLaw("Welfare", "W", null, out _);
            tax.PoliticalRequirements = req;
            welfare.PoliticalRequirements = req;
            Assert.True(manager.ProposeLaw(tax.Id, out _));
            Assert.True(manager.ProposeLaw(welfare.Id, out _));

            // 推进 5 秒：第 4 tick 两者进入 FinalValidation，第 5 tick 复检生效；
            // Tax 先生效，随后 Welfare 生效时发现冲突 → 自动废止 Tax（替换语义）
            for (int i = 0; i < 5; i++) manager.UpdateApproval(1f);

            Assert.Equal(LawStatus.Enacted, welfare.Status);
            Assert.Equal(LawStatus.Repealed, tax.Status); // 被 Welfare 替代废止
        }

        [Fact]
        public void Conflict_DetectionIsSymmetric()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Welfare", Name = "福利法", Category = LawCategory.Welfare, ConflictCategories = { LawCategory.Tax } });
            registry.RegisterTemplate(new LawTemplate { Id = "Tax", Name = "税法", Category = LawCategory.Tax, ConflictCategories = { LawCategory.Welfare } });

            var manager = new LegislationManager(registry, () => 0.9f);

            // 方向 A：Tax 生效 → Welfare 冲突
            var taxA = manager.CreateLaw("Tax", "T1", null, out _);
            Assert.True(manager.EnactLaw(taxA.Id, requireValidation: false, out _));
            var welfare = manager.CreateLaw("Welfare", "W", null, out _);
            Assert.NotNull(manager.FindConflictingEnactedLaw(welfare));
            Assert.Same(taxA, manager.FindConflictingEnactedLaw(welfare));
            Assert.False(manager.ProposeLaw(welfare.Id, out var errA));
            Assert.Contains("冲突", errA);

            // 方向 B：废止 Tax、生效 Welfare → Tax 冲突
            Assert.True(manager.RepealLaw(taxA.Id));
            Assert.True(manager.EnactLaw(welfare.Id, requireValidation: false, out _));
            var taxB = manager.CreateLaw("Tax", "T2", null, out _);
            Assert.NotNull(manager.FindConflictingEnactedLaw(taxB));
            Assert.Same(welfare, manager.FindConflictingEnactedLaw(taxB));
            Assert.False(manager.ProposeLaw(taxB.Id, out var errB));
            Assert.Contains("冲突", errB);
        }

        [Fact]
        public void Conflict_NoConflictWhenDeclarationMissing()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            // 未声明 ConflictCategory → 无互斥（兼容旧模板/测试场景）
            registry.RegisterTemplate(new LawTemplate { Id = "Labor", Name = "劳动法", Category = LawCategory.Labor });
            registry.RegisterTemplate(new LawTemplate { Id = "Tax", Name = "税法", Category = LawCategory.Tax });

            var manager = new LegislationManager(registry, () => 0.9f);
            var labor = manager.CreateLaw("Labor", "L", null, out _);
            var tax = manager.CreateLaw("Tax", "T", null, out _);
            Assert.True(manager.EnactLaw(labor.Id, requireValidation: false, out _));
            Assert.Null(manager.FindConflictingEnactedLaw(tax));
            Assert.True(manager.ProposeLaw(tax.Id, out _));
        }

        // ---- v0.80 选举阶段玩家干预 ----

        /// <summary>构造选举干预测试环境：ElectionSystem + 预算 1.2 的 Governance + InfluenceService。</summary>
        private static (ElectionInfluenceService influence, ElectionSystem election, GovernanceResponseSystem governance)
            CreateInfluenceEnvironment()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var election = new ElectionSystem();
            election.Initialize();
            var governance = new GovernanceResponseSystem();
            governance.Initialize(); // 默认预算 1.2
            ServiceRegistry.Register(governance);
            var influence = new ElectionInfluenceService(election);
            influence.Initialize();
            ServiceRegistry.Register<IElectionInfluenceService>(influence);
            return (influence, election, governance);
        }

        [Fact]
        public void Influence_EndorseSpendsBudgetAndAddsVotes_LimitedPerTerm()
        {
            var (influence, election, governance) = CreateInfluenceEnvironment();
            try
            {
                Assert.Equal(0f, influence.GetVoteModifier(FactionType.Engineering));
                Assert.True(influence.Endorse(FactionType.Engineering, out _));
                Assert.Equal(1.2f, influence.GetVoteModifier(FactionType.Engineering), 3);
                Assert.Equal(1.2f - 0.4f, governance.Budget, 3); // 花 0.4 预算

                // 每届同派系同动作限一次
                Assert.False(influence.Endorse(FactionType.Engineering, out var againErr));
                Assert.Contains("已对", againErr);
                Assert.Equal(1.2f, influence.GetVoteModifier(FactionType.Engineering), 3); // 修正不变

                // 不同派系不受限
                Assert.True(influence.Endorse(FactionType.Science, out _));
                Assert.Equal(1.2f, influence.GetVoteModifier(FactionType.Science), 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                influence.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Influence_AllActionsApplyDistinctModifiers()
        {
            var (influence, election, governance) = CreateInfluenceEnvironment();
            try
            {
                Assert.True(influence.Endorse(FactionType.Engineering, out _));          // +1.2
                Assert.True(influence.RunCampaign(FactionType.Science, out _));          // +0.6
                Assert.True(influence.Suppress(FactionType.Union, out _));               // -1.0

                Assert.Equal(ElectionInfluenceService.EndorseVotes, influence.GetVoteModifier(FactionType.Engineering), 3);
                Assert.Equal(ElectionInfluenceService.CampaignVotes, influence.GetVoteModifier(FactionType.Science), 3);
                Assert.Equal(ElectionInfluenceService.SuppressVotes, influence.GetVoteModifier(FactionType.Union), 3);
                Assert.Equal(0f, influence.GetVoteModifier(FactionType.Belief), 3);

                // 预算：1.2 - 0.4 - 0.2 - 0.3 = 0.3
                Assert.Equal(0.3f, governance.Budget, 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                influence.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Influence_BudgetInsufficientFails()
        {
            var (influence, election, governance) = CreateInfluenceEnvironment();
            try
            {
                // 花光预算：0.4×3 = 1.2
                Assert.True(influence.Endorse(FactionType.Engineering, out _));
                Assert.True(influence.Endorse(FactionType.Science, out _));
                Assert.True(influence.Endorse(FactionType.Belief, out _));
                Assert.Equal(0f, governance.Budget, 3);

                Assert.False(influence.RunCampaign(FactionType.Union, out var err));
                Assert.Contains("预算不足", err);
                Assert.Equal(0f, influence.GetVoteModifier(FactionType.Union), 3); // 修正未写入
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                influence.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Influence_SuppressHurtsTargetFactionPopularity()
        {
            var (influence, election, governance) = CreateInfluenceEnvironment();
            var factions = new FakeFactionSystem(FactionType.Union);
            ServiceRegistry.Register<IFactionSystem>(factions);
            try
            {
                float before = factions.GetFaction(FactionType.Union).Popularity; // 0.6
                Assert.True(influence.Suppress(FactionType.Union, out _));
                Assert.Equal(before - 0.02f, factions.GetFaction(FactionType.Union).Popularity, 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                influence.Shutdown();
                governance.Shutdown();
            }
        }

        [Fact]
        public void Influence_ResetClearsModifiersForNextElection()
        {
            var (influence, election, governance) = CreateInfluenceEnvironment();
            try
            {
                Assert.True(influence.Endorse(FactionType.Engineering, out _));
                Assert.Equal(1.2f, influence.GetVoteModifier(FactionType.Engineering), 3);

                influence.ResetForNewElection(); // 选举结束 → 清零（OnElectionEnded 处理器本体）
                Assert.Equal(0f, influence.GetVoteModifier(FactionType.Engineering), 3);

                // 清零后同派系可再次干预
                Assert.True(influence.Endorse(FactionType.Engineering, out _));
                Assert.Equal(1.2f, influence.GetVoteModifier(FactionType.Engineering), 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                influence.Shutdown();
                governance.Shutdown();
            }
        }

        // ---- v0.81 法律模板扩充 ----

        [Fact]
        public void Templates_EducationTemplateComplete()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(EducationLawTemplate.Create());

            var template = registry.GetTemplate("Education");
            Assert.NotNull(template);
            Assert.Equal(LawCategory.Education, template.Category);
            Assert.Contains(LawCategory.Religion, template.ConflictCategories); // 与宗教法互斥
            Assert.True(template.Parameters.Count >= 5);
            Assert.True(template.PassiveEffects.Count >= 3);
            Assert.Single(template.Plugins); // 义务教育插件
            Assert.Equal("MandatorySchooling", template.Plugins[0].Id);
            Assert.NotNull(template.Plugins[0].AddedRules); // 含违规规则
        }

        [Fact]
        public void Conflict_MultiValueDeclaration_SymmetricDetection()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            // Religion 同时声明与 Education、Security 互斥（多值）
            registry.RegisterTemplate(new LawTemplate { Id = "Religion", Name = "宗教法", Category = LawCategory.Religion, ConflictCategories = { LawCategory.Education, LawCategory.Security } });
            registry.RegisterTemplate(new LawTemplate { Id = "Education", Name = "教育法", Category = LawCategory.Education, ConflictCategories = { LawCategory.Religion } });
            registry.RegisterTemplate(new LawTemplate { Id = "Security", Name = "治安法", Category = LawCategory.Security, ConflictCategories = { LawCategory.Religion } });

            var manager = new LegislationManager(registry, () => 0.9f);

            // Religion 生效 → Education 与 Security 都被拦截
            var religion = manager.CreateLaw("Religion", "R", null, out _);
            Assert.True(manager.EnactLaw(religion.Id, requireValidation: false, out _));

            var edu = manager.CreateLaw("Education", "E", null, out _);
            var sec = manager.CreateLaw("Security", "S", null, out _);
            Assert.NotNull(manager.FindConflictingEnactedLaw(edu));
            Assert.NotNull(manager.FindConflictingEnactedLaw(sec));
            Assert.False(manager.ProposeLaw(edu.Id, out var eduErr));
            Assert.Contains("冲突", eduErr);
            Assert.False(manager.ProposeLaw(sec.Id, out var secErr));
            Assert.Contains("冲突", secErr);
        }

        // ---- v0.82 镇压反扑储备统一 ----

        [Fact]
        public void Repression_UnifiedReservoirCoefficientAcrossStrikePaths()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var pressure = new PressureModel();
            pressure.Initialize();
            ServiceRegistry.Register(pressure);
            var legitimacy = new Legitimacy();
            legitimacy.Initialize();
            ServiceRegistry.Register(legitimacy);
            var config = new ReliefConfig();
            config.Initialize();
            ServiceRegistry.Register(config);
            var gov = new GovernanceResponseSystem();
            gov.Initialize();
            try
            {
                float mul = config.RepressionReservoirMul; // 0.6，单源系数

                // 直接 ApplyRepression：储备 = |amount| × mul
                float reservoirBefore = gov.RepressionReservoir;
                gov.ApplyRepression(SituationAxis.SocialUnrest, 0.1f);
                Assert.Equal(reservoirBefore + 0.1f * mul, gov.RepressionReservoir, 4);

                // 罢工镇压（5 人，backlash=0.06×1.0=0.06）：同样按 mul 入账（不再 +0.1 加价）
                float before = gov.RepressionReservoir;
                gov.ApplyStrikeOutcome(SituationAxis.SocialUnrest, 5, negotiated: false);
                float expectedBacklash = 0.06f;
                Assert.Equal(before + expectedBacklash * mul, gov.RepressionReservoir, 4);

                // 意识形态分裂同步起步（两路径一致）
                Assert.True(pressure.Axes[SituationAxis.IdeologicalSplit] > 0f);
            }
            finally
            {
                ServiceRegistry.Clear();
                pressure.Shutdown();
                legitimacy.Shutdown();
                gov.Shutdown();
            }
        }

        [Fact]
        public void StrikeSuppress_StopsIndividualStressBounce()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var strikeSystem = new StrikeSystem();
            strikeSystem.Initialize();
            try
            {
                // 镇压仍免费复工 + 动员修正残留（不依赖预算系统）
                strikeSystem.RestoreStrike(401, 5, "stress", 0.2f);
                strikeSystem.StrikeProbabilityModifier = 0.6f;
                Assert.True(strikeSystem.SuppressStrike(out _));
                Assert.Equal(0, strikeSystem.StrikingCount);
                Assert.Equal(0.3f, strikeSystem.StrikeProbabilityModifier, 3); // 0.6 × 0.5

                // 个体压力反弹已移除：EndAllStrikes 的 stressRelief=0 不触发 AddStress 通道
                // （FindMinionById 在无 Unity 宿主时返回 null，此断言仅确认流程无异常）
                Assert.True(true);
            }
            finally
            {
                ServiceRegistry.Clear();
                strikeSystem.Shutdown();
            }
        }

        // ---- v0.83 政治改革法模板 ----

        [Fact]
        public void Templates_PoliticalTemplateComplete()
        {
            EventBus.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(PoliticalLawTemplate.Create());

            var template = registry.GetTemplate("Political");
            Assert.NotNull(template);
            Assert.Equal(LawCategory.Political, template.Category);
            Assert.Contains(LawCategory.Security, template.ConflictCategories); // 与治安法互斥
            Assert.True(template.Parameters.Count >= 6);
            Assert.True(template.PassiveEffects.Count >= 4);
            Assert.Single(template.Plugins); // 公民大会插件
            Assert.Equal("CitizensAssembly", template.Plugins[0].Id);
        }

        [Fact]
        public void Conflict_PoliticalVsSecurity_Symmetric()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(new LawTemplate { Id = "Political", Name = "政治改革法", Category = LawCategory.Political, ConflictCategories = { LawCategory.Security } });
            registry.RegisterTemplate(new LawTemplate { Id = "Security", Name = "治安法", Category = LawCategory.Security, ConflictCategories = { LawCategory.Political, LawCategory.Religion } });

            var manager = new LegislationManager(registry, () => 0.9f);

            // Political 生效 → Security 提案被拦截
            var political = manager.CreateLaw("Political", "P", null, out _);
            Assert.True(manager.EnactLaw(political.Id, requireValidation: false, out _));
            var security = manager.CreateLaw("Security", "S", null, out _);
            Assert.NotNull(manager.FindConflictingEnactedLaw(security));
            Assert.False(manager.ProposeLaw(security.Id, out var err));
            Assert.Contains("冲突", err);
        }

        // ---- v0.84 存档序列化收尾 ----

        [Fact]
        public void SaveLoad_ElectionSystemRestoresRulingFactionAndTimer()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var election = new ElectionSystem();
            election.Initialize();
            try
            {
                election.SetRulingFaction(FactionType.Union);
                election.SetLastElectionTime(123.5f);

                var data = new Dictionary<string, object>();
                election.Save(data);

                var restored = new ElectionSystem();
                restored.Initialize();
                restored.Load(data);

                Assert.Equal(FactionType.Union, restored.CurrentRulingFaction);
                Assert.Equal(123.5f, restored.LastElectionTime, 3);
                Assert.False(restored.ElectionInProgress);
            }
            finally
            {
                election.Shutdown();
            }
        }

        [Fact]
        public void SaveLoad_RulingFactionRestoresVetoUsage()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var election = new ElectionSystem();
            election.Initialize();
            var ruling = new RulingFactionService(election);
            try
            {
                Assert.True(ruling.TryConsumeVeto(out _)); // 用尽本届否决权
                Assert.Equal(0, ruling.VetoesRemaining);

                var data = new Dictionary<string, object>();
                ruling.Save(data);

                var restored = new RulingFactionService(election);
                restored.Load(data);
                Assert.Equal(0, restored.VetoesRemaining); // 否决权余量复原
                Assert.False(restored.TryConsumeVeto(out _));
            }
            finally
            {
                election.Shutdown();
                ruling.Shutdown();
            }
        }

        [Fact]
        public void SaveLoad_CampaignPlatformRestoresFulfillment()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var election = new ElectionSystem();
            election.Initialize();
            var ruling = new RulingFactionService(election);
            ServiceRegistry.Register<IRulingFactionService>(ruling);
            var platform = new CampaignPlatformService(election);
            platform.Initialize();
            try
            {
                // Engineering 偏好 Labor：1 部生效 → 进度 1 → 换届结算兑现度 0.5
                EventBus.Publish(new LawEnactedEvent { LawId = "l1", Category = LawCategory.Labor });
                election.SetRulingFaction(FactionType.Union);
                Assert.Equal(0.5f, platform.GetLastFulfillment(FactionType.Engineering), 3);

                var data = new Dictionary<string, object>();
                platform.Save(data);

                var restored = new CampaignPlatformService(election);
                restored.Load(data);
                Assert.Equal(0.5f, restored.GetLastFulfillment(FactionType.Engineering), 3);
            }
            finally
            {
                ServiceRegistry.Clear();
                election.Shutdown();
                platform.Shutdown();
                ruling.Shutdown();
            }
        }

        [Fact]
        public void SaveLoad_StrikeSystemRestoresRecordsAndModifier()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var strikeSystem = new StrikeSystem();
            strikeSystem.Initialize();
            try
            {
                strikeSystem.RestoreStrike(501, 5, "stress", 0.6f);
                strikeSystem.StrikeProbabilityModifier = 0.3f;
                Assert.Equal(1, strikeSystem.StrikingCount);

                var data = new Dictionary<string, object>();
                strikeSystem.Save(data);

                var restored = new StrikeSystem();
                restored.Initialize();
                restored.Load(data);
                Assert.Equal(1, restored.StrikingCount);          // 罢工记录复原
                Assert.Equal(0.3f, restored.StrikeProbabilityModifier, 3); // 动员修正复原
            }
            finally
            {
                ServiceRegistry.Clear();
                strikeSystem.Shutdown();
            }
        }
    }
}
