using System.Collections.Generic;
using Xunit;
using ONIModPack.Content.Legislation.Model;
using ONIModPack.Content.Legislation.Runtime;
using ONIModPack.Content.Legislation.Templates;
using ONIModPack.Content.Polity;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Governance;
using LawInstance = ONIModPack.Content.Legislation.Model.Legislation;

namespace ONIModPack.Tests
{
    /// <summary>
    /// Phase 1 经济闭环测试：税收/福利法律参数 → 财政收支 → 合法性/压力反馈。
    /// 全部走构造注入（registry / population / legitimacy / pressure），不依赖 ServiceRegistry 全局状态。
    /// </summary>
    public class ColonyTreasuryTests
    {
        private const int Pop10 = 10;

        private static LawInstance CreateLaw(string templateId, LawStatus status, Dictionary<string, float> parameters = null)
        {
            var law = new LawInstance
            {
                Id = "law-" + System.Guid.NewGuid().ToString("N"),
                TemplateId = templateId,
                Status = status
            };
            if (parameters != null)
            {
                foreach (var kv in parameters)
                    law.Parameters[kv.Key] = new ParameterValue { ParameterId = kv.Key, Value = kv.Value };
            }
            return law;
        }

        private static LegislationRegistry CreateRegistry(params LawInstance[] laws)
        {
            var registry = new LegislationRegistry();
            registry.Initialize();
            registry.RegisterTemplate(TaxLawTemplate.Create());
            registry.RegisterTemplate(WelfareLawTemplate.Create());
            foreach (var law in laws) registry.RegisterLaw(law);
            return registry;
        }

        private static ColonyTreasury CreateTreasury(LegislationRegistry registry, int pop = Pop10,
            Legitimacy legitimacy = null, PressureModel pressure = null)
        {
            var treasury = new ColonyTreasury(registry, () => pop, legitimacy, pressure);
            treasury.Initialize();
            return treasury;
        }

        // ---- 收支计算 ----

        [Fact]
        public void NoLaws_IncomeZero_ExpenseIsPublicBase()
        {
            var treasury = CreateTreasury(CreateRegistry());

            Assert.Equal(0f, treasury.ComputeIncomePerCycle(Pop10));
            Assert.Equal(Pop10 * ColonyTreasuryConfig.BasePublicExpensePerDupePerCycle,
                treasury.ComputeExpensePerCycle(Pop10));
        }

        [Fact]
        public void TaxLaw_IncomeFollowsEffectiveRate()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.2f,
                ["IncomeTaxRate"] = 0.1f
            }));
            var treasury = CreateTreasury(registry);

            treasury.Tick(0f); // 刷新 EffectiveTaxRate 缓存

            float expectedRate = 0.2f + 0.1f * ColonyTreasuryConfig.IncomeTaxWeight; // 0.25
            Assert.Equal(expectedRate, treasury.EffectiveTaxRate, 3);
            Assert.Equal(Pop10 * ColonyTreasuryConfig.BaseIncomePerDupePerCycle * expectedRate,
                treasury.ComputeIncomePerCycle(Pop10), 3); // 10 × 20 × 0.25 = 50
        }

        [Fact]
        public void TaxLaw_MissingParam_FallsBackToTemplateDefault()
        {
            // 只给 BaseTaxRate，IncomeTaxRate 缺省 → 回退模板默认 0.15
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.2f
            }));
            var treasury = CreateTreasury(registry);

            treasury.Tick(0f);

            Assert.Equal(0.2f + 0.15f * ColonyTreasuryConfig.IncomeTaxWeight, treasury.EffectiveTaxRate, 3);
        }

        [Fact]
        public void DraftOrRepealedTaxLaw_DoesNotAffectRate()
        {
            var registry = CreateRegistry(
                CreateLaw("Tax", LawStatus.Draft, new Dictionary<string, float> { ["BaseTaxRate"] = 0.5f }),
                CreateLaw("Tax", LawStatus.Repealed, new Dictionary<string, float> { ["BaseTaxRate"] = 0.9f }));
            var treasury = CreateTreasury(registry);

            treasury.Tick(0f);

            Assert.Equal(0f, treasury.EffectiveTaxRate);
        }

        [Fact]
        public void MultipleTaxLaws_Stack()
        {
            var registry = CreateRegistry(
                CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float> { ["BaseTaxRate"] = 0.1f, ["IncomeTaxRate"] = 0.2f }),
                CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float> { ["BaseTaxRate"] = 0.2f, ["IncomeTaxRate"] = 0f }));
            var treasury = CreateTreasury(registry);

            treasury.Tick(0f);

            // (0.1 + 0.1) + (0.2 + 0) = 0.4
            Assert.Equal(0.4f, treasury.EffectiveTaxRate, 3);
        }

        [Fact]
        public void WelfareLaw_AddsBenefitExpense()
        {
            var registry = CreateRegistry(CreateLaw("Welfare", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["MinimumLivingGuarantee"] = 10f,
                ["UnemploymentBenefit"] = 5f,
                ["MedicalSubsidy"] = 5f,
                ["HousingSubsidy"] = 3f,
                ["EligibilityThreshold"] = 0.5f
            }));
            var treasury = CreateTreasury(registry);

            // 公共开支 10×1 + 补助 (10+5+5+3) × 10 × 0.5 = 10 + 115 = 125
            Assert.Equal(125f, treasury.ComputeExpensePerCycle(Pop10), 3);
        }

        [Fact]
        public void TaxLaw_AddsEnforcementExpense()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["EnforcementIntensity"] = 0.5f
            }));
            var treasury = CreateTreasury(registry);

            // 公共开支 10 + 执法 5 × 0.5 × 10 = 10 + 25 = 35
            Assert.Equal(35f, treasury.ComputeExpensePerCycle(Pop10), 3);
        }

        // ---- 余额与财政反馈 ----

        [Fact]
        public void Tick_AccumulatesBalance()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.25f
            }));
            var treasury = CreateTreasury(registry);

            // 有效税率 0.25 + 0.15(默认 IncomeTaxRate)×0.5 = 0.325 → 收入 65/周期；
            // 支出 = 10 公共 + 5×0.25(默认执法力度)×10 = 22.5 → 净 +42.5/周期
            treasury.Tick(60f);

            Assert.Equal(42.5f / ColonyTreasuryConfig.CycleSeconds * 60f, treasury.Balance, 2);
        }

        [Fact]
        public void Surplus_RaisesLegitimacy()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.25f,
                ["EnforcementIntensity"] = 0f
            }));
            var legitimacy = new Legitimacy(); // Value 初始 0.6，不调 Initialize（Tick 不涉及）
            var treasury = CreateTreasury(registry, Pop10, legitimacy);

            // 收入 50/周期，支出 10 → 净 +40/周期；Tick(30) 余额 = +2 > 0.01 → 盈余采样
            treasury.Tick(30f);

            float expectedGain = ColonyTreasuryConfig.SurplusLegitimacyGainPerCycle
                                 * (ColonyTreasuryConfig.FiscalSampleSeconds / ColonyTreasuryConfig.CycleSeconds);
            Assert.Equal(0.6f + expectedGain, legitimacy.Value, 5);
        }

        [Fact]
        public void Deficit_WearsLegitimacyAndRaisesUnrest()
        {
            var registry = CreateRegistry(); // 无法律：收入 0，支出 10/周期 → 赤字
            var legitimacy = new Legitimacy();
            var pressure = new PressureModel();
            var treasury = CreateTreasury(registry, Pop10, legitimacy, pressure);

            treasury.Tick(30f); // 余额 = -0.5 < -0.01 → 赤字采样

            float sampleShare = ColonyTreasuryConfig.FiscalSampleSeconds / ColonyTreasuryConfig.CycleSeconds;
            Assert.Equal(0.6f - ColonyTreasuryConfig.DeficitLegitimacyWearPerCycle * sampleShare, legitimacy.Value, 5);
            Assert.Equal(ColonyTreasuryConfig.DeficitUnrestPerCycle * sampleShare,
                pressure.GetAxis(SituationAxis.SocialUnrest), 5);
        }

        [Fact]
        public void BalanceNearZero_NoFeedback()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.1f,
                ["EnforcementIntensity"] = 0f
            }));
            // 收入 20/周期，支出 10 → 净 +10/周期
            var legitimacy = new Legitimacy();
            var treasury = CreateTreasury(registry, Pop10, legitimacy);

            treasury.Tick(0.5f); // 余额 ≈ 0.0083，采样间隔不足 30s → 不触发反馈

            Assert.Equal(0.6f, legitimacy.Value, 5);
        }

        [Fact]
        public void PopulationZero_NoIncomeNoExpense()
        {
            var registry = CreateRegistry(CreateLaw("Tax", LawStatus.Enacted, new Dictionary<string, float>
            {
                ["BaseTaxRate"] = 0.5f
            }));
            var treasury = CreateTreasury(registry, 0);

            Assert.Equal(0f, treasury.ComputeIncomePerCycle(0));
            Assert.Equal(0f, treasury.ComputeExpensePerCycle(0));
        }

        // ---- 存档 ----

        [Fact]
        public void SaveLoad_RoundTrip()
        {
            // 注入 dummy 合法性/压力，采样反馈不触达 ServiceRegistry 全局状态（测试隔离）
            var treasury = CreateTreasury(CreateRegistry(), Pop10, new Legitimacy(), new PressureModel());
            // 无法律 → 每秒 -10/600，12.3 周期后余额 = -123
            treasury.Tick(ColonyTreasuryConfig.CycleSeconds * 12.3f);

            var data = new Dictionary<string, object>();
            treasury.Save(data);

            var restored = CreateTreasury(CreateRegistry());
            restored.Load(data);

            Assert.Equal(treasury.Balance, restored.Balance, 3);
            Assert.True(restored.Balance < 0f); // 无法律期间持续赤字
        }
    }
}
