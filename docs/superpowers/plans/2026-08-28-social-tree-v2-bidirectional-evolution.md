# 社会树 v2（双向驱动的社会演化树）实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 将原社会树从"玩家研究事件驱动的自动链"改造成"社会自然演化的双向驱动树"：支持 NPC 自然推进（Progress）、玩家主动干预点亮（Intervene，可跳过前置带社会代价 Rushed），产出社会开发点池（SDP），集成现有压力/派系/行为系统驱动方向选择。

**架构：** 新增 `SocialTreeConfig`（可外部调参）+ `SocialDevelopmentPool`（SDP 生产与消费）+ `SocialTreeProgress`（节点进度/补课/Rushed 状态跟踪 + 方向锁定）+ `SocialTreeDirectionResolver`（纯函数：信号→标签权重 + Tag→轴/话题/派系映射）；修改 `SocialResearchNode`（加 `SocialTag`）+ `SocialResearchUnlocks`（加 `InterveneNode`/`IsRushed`）+ `SocialResearchModule`（注册新服务 + 改造注入逻辑）；复用 `EventBus` 广播成熟/点亮事件，复用 `PressureModel` / `IFactionSystem` / `IPublicOpinionSystem` / `IWorkSessionLedger` 信号源，不新建管线。

**技术栈：** C# / ONI mod 主程序集（net48）+ xUnit（net8，`ONIModPack.Tests`）。`InternalsVisibleTo("ONIModPack.Tests")` 已配置（AssemblyInfo.cs）。

**上游规格：** `docs/superpowers/specs/2026-08-28-social-tree-v2-design.md`。

**关键枚举定义（规格 §七/八/六）：**
- `SocialTag`：`Mixed / Culture / Labor / Economy / Governance`（默认 `Mixed`）
- 映射表（本计划落地为准）：

  | SocialTag | 压力轴（摩擦注入） | 民望话题 | 派系偏移 |
  |-----------|-------------------|----------|----------|
  | Culture | IdeologicalSplit | Religion | Belief |
  | Labor | LaborTension | WorkConditions | Union |
  | Economy | SocialUnrest | Economy | Science |
  | Governance | MoralFatigue | Welfare | Engineering |
  | Mixed | null（跳过） | null（跳过） | 各派系均摊（不落地） |

**命令约定：** 主构建 `dotnet build ONIModPack.csproj -c Debug`；测试 `dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug`（**先 build 主项目再 test**）。

---

## 影响文件清单

| 操作 | 文件路径 | 职责 |
|------|----------|------|
| 新建 | `src/Content/SocialResearch/SocialTag.cs` | `SocialTag` 枚举 |
| 新建 | `src/Content/SocialResearch/SocialTreeEvents.cs` | `ResearchNodeMaturedEvent` / `RushedResearchEvent` |
| 新建 | `src/Content/SocialResearch/SocialTreeConfig.cs` | 配置中心，遵循 `ReliefConfig` 模式 |
| 修改 | `src/Content/Behavior/NPCFourDimensionIntegration.cs` | `MapChoreToBehavior` 提升为 `internal static`（供池/解析器共用） |
| 新建 | `src/Content/SocialResearch/SocialDevelopmentPool.cs` | SDP 池：产出速率/成本/余额/社会状态因子聚合 |
| 新建 | `src/Content/SocialResearch/SocialTreeDirectionResolver.cs` | 纯函数：信号→标签权重；Tag→轴/话题/派系映射 |
| 新建 | `src/Content/SocialResearch/SocialTreeProgress.cs` | 进度/补课/Rushed/方向锁定存储 + NPC 分配主逻辑 |
| 修改 | `src/Content/SocialResearch/SocialResearchDatabase.cs` | `SocialResearchNode` 增 `SocialTag`；21 节点打标签 |
| 修改 | `src/Content/SocialResearch/SocialResearchUnlocks.cs` | 增 `InterveneNode` / `IsRushed` / `ForceUnlock` + 冷却计数 |
| 修改 | `src/Content/SocialResearch/SocialResearchModule.cs` | 注册三个新服务，改造注入逻辑，注册主 tick |
| 新建测试 | `ONIModPack.Tests/SocialTreeConfigTests.cs` | 配置默认值/覆盖 |
| 新建测试 | `ONIModPack.Tests/SocialDevelopmentPoolTests.cs` | 速率/成本/SaveLoad |
| 新建测试 | `ONIModPack.Tests/SocialTreeDirectionResolverTests.cs` | 加权纯函数 + Tag 映射 |
| 新建测试 | `ONIModPack.Tests/SocialTreeProgressTests.cs` | 进度累加/补课/SaveLoad |
| 新建测试 | `ONIModPack.Tests/SocialTreeInterveneTests.cs` | Intervene 规则 |
| 新建测试 | `ONIModPack.Tests/SocialTreeRushedTests.cs` | Rushed 映射/补课摘除 |
| 修改 | `ONIModPack.Tests/ONIModPack.Tests.csproj` | 添加六个新测试文件 |

---

### 任务 1：新建 SocialTag 枚举和事件类

**文件：**
- 创建：`src/Content/SocialResearch/SocialTag.cs`
- 创建：`src/Content/SocialResearch/SocialTreeEvents.cs`

- [ ] **步骤 1：编写代码**

`src/Content/SocialResearch/SocialTag.cs`:
```csharp
namespace ONIModPack.Content.SocialResearch
{
    public enum SocialTag
    {
        Mixed,
        Culture,
        Labor,
        Economy,
        Governance
    }
}
```

`src/Content/SocialResearch/SocialTreeEvents.cs`:
```csharp
using ONIModPack.Core;

namespace ONIModPack.Content.SocialResearch
{
    public class ResearchNodeMaturedEvent : GameEvent
    {
        public string NodeId;
        public string NodeName;
        public SocialTag Tag;
    }

    public class RushedResearchEvent : GameEvent
    {
        public string NodeId;
        public string NodeName;
        public SocialTag Tag;
    }
}
```

- [ ] **步骤 2：Commit**

```bash
git add src/Content/SocialResearch/SocialTag.cs src/Content/SocialResearch/SocialTreeEvents.cs
git commit -m "feat(social-tree): add SocialTag enum and new events"
```

---

### 任务 2：修改 NPCFourDimensionIntegration 暴露行为映射（跨类共用）

`SocialDevelopmentPool` 与 `SocialTreeDirectionResolver` 都需要"Chore 字符串 → BehaviorType"映射。现方法为 `private` 实例方法，改为 `internal static` 跨类共用（行为映射是纯函数，改造安全）。

**文件：**
- 修改：`src/Content/Behavior/NPCFourDimensionIntegration.cs:459`

- [ ] **步骤 1：修改代码**

将第 459 行方法签名：
```csharp
        private BehaviorType? MapChoreToBehavior(string choreType)
```
改为：
```csharp
        internal static BehaviorType? MapChoreToBehavior(string choreType)
```
（方法体不变，是纯字符串映射。同文件内 388/438/517/997 行的调用点 `MapChoreToBehavior(choreType)` 语法不变，静态调用无需改动。）

- [ ] **步骤 2：构建验证**

运行：`dotnet build ONIModPack.csproj -c Debug`
预期：0 错误。

- [ ] **步骤 3：Commit**

```bash
git add src/Content/Behavior/NPCFourDimensionIntegration.cs
git commit -m "refactor(behavior): expose MapChoreToBehavior as internal static for social tree reuse"
```

---

### 任务 3：新建 SocialTreeConfig（配置中心）

**文件：**
- 创建：`src/Content/SocialResearch/SocialTreeConfig.cs`
- 创建测试：`ONIModPack.Tests/SocialTreeConfigTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写失败的测试**

创建 `ONIModPack.Tests/SocialTreeConfigTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Config;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeConfigDefaultTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var cfg = new SocialTreeConfig();
            // 不调 Initialize：默认值即为代码默认
            Assert.True(cfg.EnableNpcAutoProgress);
            Assert.True(cfg.EnablePlayerLit);
            Assert.True(cfg.AllowSkipPrereqLight);
            Assert.Equal(1.6f, cfg.SkipPrereqCostMult);
            Assert.Equal(1.0f, cfg.InterveneCostMult);
            Assert.Equal(2, cfg.InterveneCooldownCycles);
            Assert.Equal(0.05f, cfg.BaseRate);
            Assert.Equal(0.45f, cfg.PopExponent);
            Assert.Equal(0.5f, cfg.SWPressure);
            Assert.Equal(0.3f, cfg.SWFaction);
            Assert.Equal(0.2f, cfg.SWBehavior);
            Assert.Equal(0.04f, cfg.SpendFractionPerTick);
            Assert.Equal(0.06f, cfg.ExpansionPerNode);
            Assert.Equal(1.0f, cfg.TierMulT1);
            Assert.Equal(1.15f, cfg.TierMulT2);
            Assert.Equal(1.35f, cfg.TierMulT3);
            Assert.Equal(1.6f, cfg.TierMulT4);
            Assert.Equal(1.9f, cfg.TierMulT5);
            Assert.Equal(0.3f, cfg.RushedEfficiencyPenalty);
            Assert.Equal(0.002f, cfg.RushedFrictionPerSec);
            Assert.Equal(0.002f, cfg.RushedOpinionDrop);
            Assert.Equal(2.0f, cfg.RemediationFavor);
            Assert.Equal(200f, cfg.ResearchBoost);
        }
    }

    public class SocialTreeConfigOverrideTests
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
            fake.SetConfigValue("Content.SocialResearch", "SocialTree.SkipPrereqCostMult", 2.0f);
            ServiceRegistry.Register<IConfigManager>(fake);

            var cfg = new SocialTreeConfig();
            cfg.Initialize();

            Assert.Equal(2.0f, cfg.SkipPrereqCostMult);
            Assert.Equal(1.0f, cfg.InterveneCostMult); // 未覆盖键保持默认
            ServiceRegistry.Clear();
        }
    }
}
```

创建 `ONIModPack.Tests/ONIModPack.Tests.csproj` 前先读取其内容，然后在 Compile ItemGroup 内、`ElectionTermResetC2Tests.cs` 之后追加一行：
```xml
    <Compile Include="SocialTreeConfigTests.cs" />
```

- [ ] **步骤 2：运行测试验证失败**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeConfig"
```
预期：FAIL，`The type or namespace name 'SocialTreeConfig' does not exist`。

- [ ] **步骤 3：编写实现代码**

创建 `src/Content/SocialResearch/SocialTreeConfig.cs`:

```csharp
using ONIModPack.Core;
using ONIModPack.Core.Config;
using ONIModPack.Core.Services;

namespace ONIModPack.Content.SocialResearch
{
    /// <summary>
    /// 社会树 v2 调参中心（遵循 ReliefConfig 模式）。
    /// 全部参数可经 IConfigManager（module="Content.SocialResearch"，键前缀 "SocialTree."）外部覆盖；
    /// 未配置时回落代码默认值。
    /// </summary>
    public class SocialTreeConfig : IService
    {
        private const string MODULE = "Content.SocialResearch";
        private const string KEY = "SocialTree.";

        // 功能开关
        public bool EnableNpcAutoProgress = true;
        public bool EnablePlayerLit = true;
        public bool AllowSkipPrereqLight = true;

        // 成本系数
        public float SkipPrereqCostMult = 1.6f;
        public float InterveneCostMult = 1.0f;
        public int InterveneCooldownCycles = 2;

        // 产出与方向加权
        public float BaseRate = 0.05f;         // /s
        public float PopExponent = 0.45f;      // 人口因子幂
        public float SWPressure = 0.5f;        // 方向加权中压力轴比例
        public float SWFaction = 0.3f;         // 方向加权中派系比例
        public float SWBehavior = 0.2f;        // 方向加权中行为构成比例
        public float SpendFractionPerTick = 0.04f; // NPC 每 tick 支出占池子比例

        // 成本递增曲线
        public float ExpansionPerNode = 0.06f;  // 每点亮一节点总成本增幅
        public float TierMulT1 = 1.0f;
        public float TierMulT2 = 1.15f;
        public float TierMulT3 = 1.35f;
        public float TierMulT4 = 1.6f;
        public float TierMulT5 = 1.9f;

        // Rushed 社会代价
        public float RushedEfficiencyPenalty = 0.3f;   // Rushed 解锁物能力折扣（0.3=打七折）
        public float RushedFrictionPerSec = 0.002f;    // Rushed 摩擦注入速率（每 Rushed 节点）
        public float RushedOpinionDrop = 0.002f;       // Rushed 周期事件民望下降量（一次性 per cycle）
        public float RemediationFavor = 2.0f;          // Rushed 补课优先分配倍率

        // 注入量
        public float ResearchBoost = 200f;  // 玩家研究完成 → SDP 注入量

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            LoadFromConfigManager();
            IsInitialized = true;
            ModLogger.Info($"[SocialTree] SocialTreeConfig initialized" +
                $" | enableAuto={EnableNpcAutoProgress} enablePlayer={EnablePlayerLit}" +
                $" | baseRate={BaseRate} popExp={PopExponent}" +
                $" | spendFrac={SpendFractionPerTick} expansion={ExpansionPerNode}");
        }

        public void Shutdown() => IsInitialized = false;

        private void LoadFromConfigManager()
        {
            var cfg = ServiceResolver.OptionalService<IConfigManager>();
            if (cfg == null) return;

            EnableNpcAutoProgress = cfg.GetConfigValue(MODULE, KEY + nameof(EnableNpcAutoProgress), EnableNpcAutoProgress);
            EnablePlayerLit = cfg.GetConfigValue(MODULE, KEY + nameof(EnablePlayerLit), EnablePlayerLit);
            AllowSkipPrereqLight = cfg.GetConfigValue(MODULE, KEY + nameof(AllowSkipPrereqLight), AllowSkipPrereqLight);
            SkipPrereqCostMult = cfg.GetConfigValue(MODULE, KEY + nameof(SkipPrereqCostMult), SkipPrereqCostMult);
            InterveneCostMult = cfg.GetConfigValue(MODULE, KEY + nameof(InterveneCostMult), InterveneCostMult);
            InterveneCooldownCycles = cfg.GetConfigValue(MODULE, KEY + nameof(InterveneCooldownCycles), InterveneCooldownCycles);
            BaseRate = cfg.GetConfigValue(MODULE, KEY + nameof(BaseRate), BaseRate);
            PopExponent = cfg.GetConfigValue(MODULE, KEY + nameof(PopExponent), PopExponent);
            SWPressure = cfg.GetConfigValue(MODULE, KEY + nameof(SWPressure), SWPressure);
            SWFaction = cfg.GetConfigValue(MODULE, KEY + nameof(SWFaction), SWFaction);
            SWBehavior = cfg.GetConfigValue(MODULE, KEY + nameof(SWBehavior), SWBehavior);
            SpendFractionPerTick = cfg.GetConfigValue(MODULE, KEY + nameof(SpendFractionPerTick), SpendFractionPerTick);
            ExpansionPerNode = cfg.GetConfigValue(MODULE, KEY + nameof(ExpansionPerNode), ExpansionPerNode);
            TierMulT1 = cfg.GetConfigValue(MODULE, KEY + nameof(TierMulT1), TierMulT1);
            TierMulT2 = cfg.GetConfigValue(MODULE, KEY + nameof(TierMulT2), TierMulT2);
            TierMulT3 = cfg.GetConfigValue(MODULE, KEY + nameof(TierMulT3), TierMulT3);
            TierMulT4 = cfg.GetConfigValue(MODULE, KEY + nameof(TierMulT4), TierMulT4);
            TierMulT5 = cfg.GetConfigValue(MODULE, KEY + nameof(TierMulT5), TierMulT5);
            RushedEfficiencyPenalty = cfg.GetConfigValue(MODULE, KEY + nameof(RushedEfficiencyPenalty), RushedEfficiencyPenalty);
            RushedFrictionPerSec = cfg.GetConfigValue(MODULE, KEY + nameof(RushedFrictionPerSec), RushedFrictionPerSec);
            RushedOpinionDrop = cfg.GetConfigValue(MODULE, KEY + nameof(RushedOpinionDrop), RushedOpinionDrop);
            RemediationFavor = cfg.GetConfigValue(MODULE, KEY + nameof(RemediationFavor), RemediationFavor);
            ResearchBoost = cfg.GetConfigValue(MODULE, KEY + nameof(ResearchBoost), ResearchBoost);
        }
    }
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeConfig"
```
预期：两个测试全 PASS。

- [ ] **步骤 5：Commit**

```bash
git add src/Content/SocialResearch/SocialTreeConfig.cs ONIModPack.Tests/SocialTreeConfigTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "feat(social-tree): add SocialTreeConfig with tests"
```

---

### 任务 4：修改 SocialResearchDatabase 加入 SocialTag 并标注 21 节点

**文件：**
- 修改：`src/Content/SocialResearch/SocialResearchDatabase.cs`

- [ ] **步骤 1：修改代码**

在 `SocialResearchNode` 类第 320 行（`public bool IsUnlocked;`）之后新增字段：

```csharp
        public SocialTag SocialTag;
```

按以下表格为每个 `AddNode` 调用设置 `SocialTag`：

| 节点 | SocialTag |
|------|-----------|
| SocialObservation | SocialTag.Mixed |
| MassPsychology | SocialTag.Culture |
| GroupDynamics | SocialTag.Labor |
| BasicPropaganda | SocialTag.Culture |
| OrganizationalTheory | SocialTag.Labor |
| LaborManagement | SocialTag.Labor |
| UnionSystem | SocialTag.Labor |
| Specialization | SocialTag.Economy |
| WorkScheduling | SocialTag.Labor |
| SocialEngineering | SocialTag.Culture |
| PropagandaEngineering | SocialTag.Culture |
| CulturalShaping | SocialTag.Culture |
| ColonyLaws | SocialTag.Governance |
| ResourceAllocation | SocialTag.Economy |
| PublicWelfare | SocialTag.Governance |
| IdeologySystem | SocialTag.Culture |
| Technocracy | SocialTag.Economy |
| ArtificialGovernance | SocialTag.Governance |
| ColonyParliament | SocialTag.Governance |
| SocialSimulation | SocialTag.Governance |
| ColonyGovernance | SocialTag.Governance |

完整示例（第一个节点应改为）：
```csharp
            AddNode(new SocialResearchNode
            {
                Id = "SocialObservation",
                Name = "Social Observation",
                Description = "Observe interaction patterns between Duplicants",
                Cost = 500,
                Tier = 1,
                UnlockedBuildings = new[] { "CommunityCenter" },
                Unlocks = new[] { "MassPsychology", "GroupDynamics" },
                Dependencies = new string[] { },
                SocialTag = SocialTag.Mixed
            });
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build ONIModPack.csproj -c Debug`
预期：0 错误。

- [ ] **步骤 3：Commit**

```bash
git add src/Content/SocialResearch/SocialResearchDatabase.cs
git commit -m "refactor(social-tree): tag all 21 nodes with SocialTag"
```

---

### 任务 5：新建 SocialDevelopmentPool（SDP 社会开发点池）

**文件：**
- 创建：`src/Content/SocialResearch/SocialDevelopmentPool.cs`
- 创建测试：`ONIModPack.Tests/SocialDevelopmentPoolTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写失败的测试**

创建 `ONIModPack.Tests/SocialDevelopmentPoolTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialDevelopmentPoolRateTests
    {
        private void RegisterConfig()
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig();
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);
        }

        [Fact]
        public void ComputeRate_ZeroPopulation_ReturnsBaseRate()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            // 0 人口按 1 计 → f(人口)=1
            float rate = pool.ComputeCurrentRate(0, 0f, 0f, 0f);
            // f(社会状态)=1+0=1 → rate = 0.05*1*1
            Assert.Equal(0.05f, rate, 4);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ComputeRate_WithSocialFactors_AppliesMultipliers()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            float rate = pool.ComputeCurrentRate(16, 0.5f, 0.5f, 0.5f);
            // f(人口) = 16^0.45 ≈ 3.4820
            // f(社会状态) = 1 + 0.5*0.5 + 0.3*0.5 + 0.2*0.5 = 1.5
            // rate = 0.05 * 3.4820 * 1.5 ≈ 0.2612
            Assert.Equal(0.2612f, rate, 2);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ComputeNodeCost_AppliesTierAndExpansion()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            // T1 node，0 点亮 → 500 * 1.0 * (1 + 0.06*0) = 500
            Assert.Equal(500f, pool.ComputeNodeCost(500, 1, 0), 1);
            // T2 node，5 点亮 → 800 * 1.15 * (1 + 0.06*5) = 920 * 1.3 = 1196
            Assert.Equal(1196f, pool.ComputeNodeCost(800, 2, 5), 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void CanSpend_ChecksBalance()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1000f);
            Assert.True(pool.CanSpend(500f));
            Assert.False(pool.CanSpend(1500f));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void TrySpend_ChargesBalance_Correctly()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1000f);
            bool ok = pool.TrySpend(600f);
            Assert.True(ok);
            Assert.Equal(400f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SaveLoad_RoundTrips_Balance()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1234.5f);

            var data = new Dictionary<string, object>();
            pool.Save(data);
            pool.Load(data);

            Assert.Equal(1234.5f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void Load_NoBalanceKey_SetsZero()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(100f);

            var data = new Dictionary<string, object>();
            pool.Load(data);

            Assert.Equal(0f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }
    }
}
```

在 `ONIModPack.Tests/ONIModPack.Tests.csproj` 追加：
```xml
    <Compile Include="SocialDevelopmentPoolTests.cs" />
```

- [ ] **步骤 2：运行测试验证失败**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialDevelopmentPool"
```
预期：FAIL，类型不存在。

- [ ] **步骤 3：编写实现代码**

创建 `src/Content/SocialResearch/SocialDevelopmentPool.cs`:

```csharp
using System;
using System.Collections.Generic;
using ONIModPack.Core;
using ONIModPack.Core.Services;
using ONIModPack.Content.Behavior;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.WorkSessionLedger;

namespace ONIModPack.Content.SocialResearch
{
    /// <summary>
    /// 社会开发点池（SDP）：产出速率计算、成本计算、余额存储、社会状态因子聚合。
    /// SaveId: "SocialTreeDevelopmentPool"
    /// </summary>
    public class SocialDevelopmentPool : IService, ISaveable
    {
        private float _currentBalance;
        private SocialTreeConfig _config;

        public float CurrentBalance => _currentBalance;
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _config = ServiceResolver.OptionalService<SocialTreeConfig>();
            _currentBalance = 0f;
            IsInitialized = true;
            ModLogger.Debug("[SocialTree] SocialDevelopmentPool initialized");
        }

        public void Shutdown()
        {
            _currentBalance = 0f;
            _config = null;
            IsInitialized = false;
        }

        /// <summary>rate = BaseRate × f(人口) × f(社会状态)</summary>
        public float ComputeCurrentRate(int maxDupes, float pressureAvg, float factionInfluence, float behaviorActivity)
        {
            if (_config == null) return 0f;
            float fPop = maxDupes <= 0 ? 1.0f : (float)Math.Pow(maxDupes, _config.PopExponent);
            float fSocial = 1f + pressureAvg * _config.SWPressure
                              + factionInfluence * _config.SWFaction
                              + behaviorActivity * _config.SWBehavior;
            return _config.BaseRate * fPop * fSocial;
        }

        /// <summary>nodeCost = baseCost × tierMul × (1 + ExpansionPerNode × unlockedCount)</summary>
        public float ComputeNodeCost(int baseCost, int tier, int unlockedCount)
        {
            if (_config == null) return baseCost;
            float tierMul = GetTierMultiplier(tier);
            float expansion = 1f + _config.ExpansionPerNode * unlockedCount;
            return baseCost * tierMul * expansion;
        }

        private float GetTierMultiplier(int tier)
        {
            switch (tier)
            {
                case 1: return _config.TierMulT1;
                case 2: return _config.TierMulT2;
                case 3: return _config.TierMulT3;
                case 4: return _config.TierMulT4;
                case 5: return _config.TierMulT5;
                default: return _config.TierMulT1;
            }
        }

        public bool CanSpend(float cost) => _currentBalance >= cost;

        public bool TrySpend(float cost)
        {
            if (!CanSpend(cost)) return false;
            _currentBalance -= cost;
            return true;
        }

        public void AddBalance(float amount) => _currentBalance += amount;

        /// <summary>
        /// 聚合现有系统信号为 (压力均值, 派系影响力, 行为活跃度) 三元组。
        /// 缺服务时对应项返回安全默认（压力 0、派系 0、行为 0.5）。
        /// </summary>
        public (float pressureAvg, float factionInfluence, float behaviorActivity) GetSocialStateFactors()
        {
            float pressureAvg = 0f;
            var pressureModel = ServiceResolver.OptionalService<PressureModel>();
            if (pressureModel != null)
            {
                float sum = 0f;
                foreach (SituationAxis axis in Enum.GetValues(typeof(SituationAxis)))
                    sum += pressureModel.GetAxis(axis);
                pressureAvg = sum / 4f; // 4 根轴归一化均值 0~1
            }

            float factionInfluence = 0f;
            var factionSystem = ServiceResolver.OptionalService<IFactionSystem>();
            var factionTypes = (FactionType[])Enum.GetValues(typeof(FactionType));
            if (factionSystem != null)
            {
                float sum = 0f;
                foreach (var type in factionTypes)
                {
                    var data = factionSystem.GetFaction(type);
                    if (data != null) sum += data.Influence;
                }
                factionInfluence = sum / factionTypes.Length; // 归一化 0~1
            }

            float behaviorActivity = 0.5f; // 缺省回退
            var ledger = ServiceResolver.OptionalService<IWorkSessionLedger>();
            if (ledger != null)
            {
                var snapshots = ledger.GetAllLastSessions();
                int total = 0;
                foreach (var kvp in snapshots)
                {
                    var snap = kvp.Value;
                    if (snap == null || string.IsNullOrEmpty(snap.ChoreType)) continue;
                    var behavior = NPCFourDimensionIntegration.MapChoreToBehavior(snap.ChoreType);
                    if (behavior.HasValue) total++;
                }
                if (snapshots.Count > 0)
                    behaviorActivity = (float)total / snapshots.Count; // 已归类会话占比 → 活跃度
            }

            return (pressureAvg, factionInfluence, behaviorActivity);
        }

        public string SaveId => "SocialTreeDevelopmentPool";

        public void Save(Dictionary<string, object> data) => data["Balance"] = _currentBalance;

        public void Load(Dictionary<string, object> data)
        {
            if (data.TryGetValue("Balance", out var obj) && obj is float bal)
                _currentBalance = bal;
            else
                _currentBalance = 0f;
        }

        public void Reset() => _currentBalance = 0f;
    }
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialDevelopmentPool"
```
预期：所有测试 PASS。

- [ ] **步骤 5：Commit**

```bash
git add src/Content/SocialResearch/SocialDevelopmentPool.cs ONIModPack.Tests/SocialDevelopmentPoolTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "feat(social-tree): add SocialDevelopmentPool with tests"
```

---

### 任务 6：新建 SocialTreeDirectionResolver（纯函数：加权 + Tag 映射）

**文件：**
- 创建：`src/Content/SocialResearch/SocialTreeDirectionResolver.cs`
- 创建测试：`ONIModPack.Tests/SocialTreeDirectionResolverTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写失败的测试**

创建 `ONIModPack.Tests/SocialTreeDirectionResolverTests.cs`:

```csharp
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
```

在 `ONIModPack.Tests/ONIModPack.Tests.csproj` 追加：
```xml
    <Compile Include="SocialTreeDirectionResolverTests.cs" />
```

- [ ] **步骤 2：运行测试验证失败**

预期：FAIL，类型/成员不存在。

- [ ] **步骤 3：编写实现代码**

创建 `src/Content/SocialResearch/SocialTreeDirectionResolver.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ONIModPack.Content.Behavior;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Content.SocialResearch
{
    /// <summary>
    /// 纯函数：信号 → 标签权重 + SocialTag → 轴/话题/派系映射。
    /// </summary>
    public static class SocialTreeDirectionResolver
    {
        /// <summary>
        /// 从三类信号计算各 SocialTag 权重（和 = 1；全空 → 等权）。
        /// 映射规则（规格 §八）：
        /// - 压力轴：LaborTension→Labor；IdeologicalSplit→Culture；MoralFatigue→Governance；SocialUnrest→Governance
        /// - 派系：Union→Labor；Belief→Culture；Engineering→Governance；Science→Economy
        /// - 行为：Work/Explore→Labor；Create→Culture；Study→Economy；Socialize/Rest/Play/Relax→Governance
        /// </summary>
        public static Dictionary<SocialTag, float> ComputeWeights(
            IReadOnlyDictionary<SituationAxis, float> pressureSignals,
            IReadOnlyDictionary<FactionType, float> factionSignals,
            IReadOnlyDictionary<BehaviorType, int> behaviorCounts,
            SocialTreeConfig config)
        {
            var weights = new Dictionary<SocialTag, float>
            {
                [SocialTag.Mixed] = 0f,
                [SocialTag.Culture] = 0f,
                [SocialTag.Labor] = 0f,
                [SocialTag.Economy] = 0f,
                [SocialTag.Governance] = 0f
            };

            if (pressureSignals != null)
            {
                foreach (var kvp in pressureSignals)
                {
                    var tag = MapAxisToTag(kvp.Key);
                    if (tag.HasValue) weights[tag.Value] += kvp.Value * config.SWPressure;
                }
            }

            if (factionSignals != null)
            {
                foreach (var kvp in factionSignals)
                {
                    var tag = MapFactionToTag(kvp.Key);
                    if (tag.HasValue) weights[tag.Value] += kvp.Value * config.SWFaction;
                }
            }

            if (behaviorCounts != null && behaviorCounts.Count > 0)
            {
                int total = behaviorCounts.Values.Sum();
                if (total > 0)
                {
                    foreach (var kvp in behaviorCounts)
                    {
                        var tag = MapBehaviorToTag(kvp.Key);
                        if (tag.HasValue)
                            weights[tag.Value] += (float)kvp.Value / total * config.SWBehavior;
                    }
                }
            }

            float sum = weights.Values.Sum();
            if (sum <= 0.0001f)
            {
                float equal = 1f / weights.Count;
                foreach (var tag in weights.Keys.ToList()) weights[tag] = equal;
            }
            else
            {
                foreach (var tag in weights.Keys.ToList()) weights[tag] /= sum;
            }

            return weights;
        }

        private static SocialTag? MapAxisToTag(SituationAxis axis)
        {
            switch (axis)
            {
                case SituationAxis.LaborTension: return SocialTag.Labor;
                case SituationAxis.IdeologicalSplit: return SocialTag.Culture;
                case SituationAxis.MoralFatigue: return SocialTag.Governance;
                case SituationAxis.SocialUnrest: return SocialTag.Governance;
                default: return null;
            }
        }

        private static SocialTag? MapFactionToTag(FactionType type)
        {
            switch (type)
            {
                case FactionType.Union: return SocialTag.Labor;
                case FactionType.Belief: return SocialTag.Culture;
                case FactionType.Engineering: return SocialTag.Governance;
                case FactionType.Science: return SocialTag.Economy;
                default: return null;
            }
        }

        private static SocialTag? MapBehaviorToTag(BehaviorType type)
        {
            switch (type)
            {
                case BehaviorType.Work: return SocialTag.Labor;
                case BehaviorType.Explore: return SocialTag.Labor;
                case BehaviorType.Create: return SocialTag.Culture;
                case BehaviorType.Study: return SocialTag.Economy;
                case BehaviorType.Socialize: return SocialTag.Governance;
                case BehaviorType.Rest: return SocialTag.Governance;
                case BehaviorType.Play: return SocialTag.Governance;
                case BehaviorType.Relax: return SocialTag.Governance;
                default: return null;
            }
        }

        // ---- Rushed 副作用映射（规格 §六） ----

        public static SituationAxis? MapTagToAxis(SocialTag tag)
        {
            switch (tag)
            {
                case SocialTag.Culture: return SituationAxis.IdeologicalSplit;
                case SocialTag.Labor: return SituationAxis.LaborTension;
                case SocialTag.Economy: return SituationAxis.SocialUnrest;
                case SocialTag.Governance: return SituationAxis.MoralFatigue;
                default: return null; // Mixed 跳过摩擦
            }
        }

        public static OpinionTopic? MapTagToOpinionTopic(SocialTag tag)
        {
            switch (tag)
            {
                case SocialTag.Culture: return OpinionTopic.Religion;
                case SocialTag.Labor: return OpinionTopic.WorkConditions;
                case SocialTag.Economy: return OpinionTopic.Economy;
                case SocialTag.Governance: return OpinionTopic.Welfare;
                default: return null; // Mixed 跳过民望
            }
        }

        public static FactionType? MapTagToFaction(SocialTag tag)
        {
            switch (tag)
            {
                case SocialTag.Culture: return FactionType.Belief;
                case SocialTag.Labor: return FactionType.Union;
                case SocialTag.Economy: return FactionType.Science;
                case SocialTag.Governance: return FactionType.Engineering;
                default: return null; // Mixed 各派系均摊（本版本不落地）
            }
        }
    }
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeDirectionResolver"
```
预期：所有测试 PASS。

- [ ] **步骤 5：Commit**

```bash
git add src/Content/SocialResearch/SocialTreeDirectionResolver.cs ONIModPack.Tests/SocialTreeDirectionResolverTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "feat(social-tree): add SocialTreeDirectionResolver with tests"
```

---

### 任务 7：新建 SocialTreeProgress（进度/补课/Rushed/方向锁定 + NPC 分配）

**文件：**
- 创建：`src/Content/SocialResearch/SocialTreeProgress.cs`
- 创建测试：`ONIModPack.Tests/SocialTreeProgressTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写失败的测试**

创建 `ONIModPack.Tests/SocialTreeProgressTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeProgressTests
    {
        private void SetupServices()
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig();
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);

            var db = new SocialResearchDatabase();
            db.Initialize();
            ServiceRegistry.Register<SocialResearchDatabase>(db);

            var tree = new SocialResearchTree();
            tree.Initialize();
            ServiceRegistry.Register<SocialResearchTree>(tree);

            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            ServiceRegistry.Register<SocialDevelopmentPool>(pool);

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            ServiceRegistry.Register<SocialResearchUnlocks>(unlocks);
        }

        [Fact]
        public void NewNode_ProgressZero_NotRushed()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();

            Assert.Equal(0f, progress.GetProgress("X"));
            Assert.False(progress.IsRushed("X"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void AddProgress_AccumulatesAndClampsToOne()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();

            progress.AddProgress("n1", 0.6f);
            progress.AddProgress("n1", 0.6f);

            Assert.Equal(1f, progress.GetProgress("n1"), 3);
            Assert.True(progress.IsReadyToMature("n1"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void Rushed_MarkAndRemediate()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.MarkRushed("n1");

            Assert.True(progress.IsRushed("n1"));
            Assert.Equal(0f, progress.GetRemediation("n1"));

            progress.AddRemediation("n1", 0.5f);
            Assert.Equal(0.5f, progress.GetRemediation("n1"), 3);
            Assert.True(progress.IsRushed("n1")); // 未满 → 仍 Rushed

            progress.AddRemediation("n1", 0.5f);
            Assert.True(progress.IsRemediationComplete("n1"));
            Assert.False(progress.IsRushed("n1")); // 补课完成 → 摘除
            ServiceRegistry.Clear();
        }

        [Fact]
        public void DirectionLock_SetAndQuery()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.SetDirectionLock(SocialTag.Labor, true);

            Assert.True(progress.IsDirectionLocked(SocialTag.Labor));
            Assert.True(progress.IsDirectionLocked(SocialTag.Governance) == false);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SaveLoad_RoundTrips_AllState()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.AddProgress("n1", 0.5f);
            progress.MarkRushed("n1");
            progress.AddRemediation("n1", 0.3f);
            progress.SetDirectionLock(SocialTag.Labor, true);

            var data = new Dictionary<string, object>();
            progress.Save(data);
            progress.Load(data);

            Assert.Equal(0.5f, progress.GetProgress("n1"), 3);
            Assert.True(progress.IsRushed("n1"));
            Assert.Equal(0.3f, progress.GetRemediation("n1"), 3);
            Assert.True(progress.IsDirectionLocked(SocialTag.Labor));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void AllocateProgress_WithBalance_MaturesEligibleNode()
        {
            SetupServices();
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(100000f); // 大幅充足余额

            var progress = new SocialTreeProgress();
            progress.Initialize();

            // SocialObservation 无依赖、T1、cost 500；单次分配 4% = 4000 → progress 达 8（>1）→ 成熟解锁
            bool matured = progress.AllocateProgress();
            Assert.True(matured);
            Assert.True(ServiceResolver.Get<SocialResearchUnlocks>().IsResearchUnlocked("SocialObservation"));
            ServiceRegistry.Clear();
        }
    }
}
```

在 `ONIModPack.Tests/ONIModPack.Tests.csproj` 追加：
```xml
    <Compile Include="SocialTreeProgressTests.cs" />
```

- [ ] **步骤 2：运行测试验证失败**

预期：FAIL，类型不存在。

- [ ] **步骤 3：编写实现代码**

创建 `src/Content/SocialResearch/SocialTreeProgress.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ONIModPack.Core;
using ONIModPack.Core.Services;
using ONIModPack.Content.Behavior;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.WorkSessionLedger;

namespace ONIModPack.Content.SocialResearch
{
    /// <summary>
    /// 社会树进度系统：节点 Progress/Remediation/Rushed 状态、方向锁定，以及 NPC 分配主逻辑。
    /// SaveId: "SocialTreeProgress"
    /// </summary>
    public class SocialTreeProgress : IService, ISaveable
    {
        private readonly Dictionary<string, float> _progress = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _remediation = new Dictionary<string, float>();
        private readonly HashSet<string> _rushed = new HashSet<string>();
        private readonly HashSet<SocialTag> _lockedDirections = new HashSet<SocialTag>();

        private SocialTreeConfig _config;
        private SocialDevelopmentPool _pool;
        private SocialResearchDatabase _db;
        private SocialResearchUnlocks _unlocks;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            _config = ServiceResolver.OptionalService<SocialTreeConfig>();
            _pool = ServiceResolver.OptionalService<SocialDevelopmentPool>();
            _db = ServiceResolver.OptionalService<SocialResearchDatabase>();
            _unlocks = ServiceResolver.OptionalService<SocialResearchUnlocks>();
            IsInitialized = true;
            ModLogger.Debug("[SocialTree] SocialTreeProgress initialized");
        }

        public void Shutdown()
        {
            _progress.Clear();
            _remediation.Clear();
            _rushed.Clear();
            _lockedDirections.Clear();
            _config = null;
            _pool = null;
            _db = null;
            _unlocks = null;
            IsInitialized = false;
        }

        // -- 进度 --
        public float GetProgress(string nodeId)
            => _progress.TryGetValue(nodeId, out var p) ? p : 0f;

        public void AddProgress(string nodeId, float amount)
        {
            if (!_progress.TryGetValue(nodeId, out var cur)) cur = 0f;
            _progress[nodeId] = Math.Min(1f, cur + amount);
        }

        public bool IsReadyToMature(string nodeId) => GetProgress(nodeId) >= 1f - 0.0001f;

        // -- Rushed / 补课 --
        public bool IsRushed(string nodeId) => _rushed.Contains(nodeId);

        public void MarkRushed(string nodeId) => _rushed.Add(nodeId);

        public float GetRemediation(string nodeId)
            => _remediation.TryGetValue(nodeId, out var r) ? r : 0f;

        public void AddRemediation(string nodeId, float amount)
        {
            if (!_remediation.TryGetValue(nodeId, out var cur)) cur = 0f;
            cur = Math.Min(1f, cur + amount);
            _remediation[nodeId] = cur;
            if (cur >= 1f - 0.0001f)
                _rushed.Remove(nodeId); // 补课完成 → 摘除 Rushed
        }

        public bool IsRemediationComplete(string nodeId) => GetRemediation(nodeId) >= 1f - 0.0001f;

        // -- 方向锁定 --
        public bool IsDirectionLocked(SocialTag tag) => _lockedDirections.Contains(tag);

        public void SetDirectionLock(SocialTag tag, bool locked)
        {
            if (locked) _lockedDirections.Add(tag);
            else _lockedDirections.Remove(tag);
        }

        public IReadOnlyCollection<SocialTag> LockedDirections => _lockedDirections.ToList();

        /// <summary>
        /// NPC 自动推进（每 tick 一次）：
        /// 1) 支出额 = 池子余额 × SpendFractionPerTick；2) 候选=依赖满足且未解锁；3) 排序分配；4) 成熟即解锁。
        /// 返回是否有节点成熟解锁。
        /// </summary>
        public bool AllocateProgress()
        {
            if (_config == null || _pool == null || _db == null || _unlocks == null)
                return false;
            if (!_config.EnableNpcAutoProgress)
                return false;

            float spend = _pool.CurrentBalance * _config.SpendFractionPerTick;
            if (spend <= 0.001f)
                return false;

            var candidates = GetCandidateNodes();
            if (candidates.Count == 0)
                return false;

            var ordered = OrderCandidates(candidates);

            bool anyMatured = false;
            float remainingSpend = spend;
            foreach (var (node, isRushedRemediation) in ordered)
            {
                if (remainingSpend <= 0.001f) break;

                float cost = _pool.ComputeNodeCost(node.Cost, node.Tier, _unlocks.UnlockedResearch.Count);
                float alloc = remainingSpend / cost; // 本次支出可折合的进度
                float spendHere = alloc * cost;

                AddProgress(node.Id, alloc);
                if (isRushedRemediation)
                    AddRemediation(node.Id, alloc * _config.RemediationFavor);

                _pool.TrySpend(spendHere);
                remainingSpend -= spendHere;

                if (IsReadyToMature(node.Id) && !node.IsUnlocked)
                {
                    if (_unlocks.UnlockResearch(node.Id))
                    {
                        anyMatured = true;
                        EventBus.Publish(new ResearchNodeMaturedEvent
                        {
                            NodeId = node.Id,
                            NodeName = node.Name,
                            Tag = node.SocialTag
                        });
                        ModLogger.Info($"[SocialTree] Node matured: {node.Name}");
                    }
                }
            }
            return anyMatured;
        }

        private List<SocialResearchNode> GetCandidateNodes()
        {
            var result = new List<SocialResearchNode>();
            foreach (var node in _db.ResearchNodes.Values)
            {
                if (node.IsUnlocked) continue;
                bool depsMet = true;
                foreach (var dep in node.Dependencies)
                {
                    if (!_unlocks.IsResearchUnlocked(dep)) { depsMet = false; break; }
                }
                if (!depsMet) continue;
                // 方向锁定激活时，仅锁定标签的候选可分配
                if (_lockedDirections.Count > 0 && !_lockedDirections.Contains(node.SocialTag)) continue;
                result.Add(node);
            }
            return result;
        }

        private List<(SocialResearchNode node, bool isRushedRemediation)> OrderCandidates(List<SocialResearchNode> candidates)
        {
            var (pressureAvg, factionInfluence, behaviorActivity) = _pool.GetSocialStateFactors();

            var pressureDict = new Dictionary<SituationAxis, float>();
            var pressureModel = ServiceResolver.OptionalService<PressureModel>();
            if (pressureModel != null)
            {
                foreach (SituationAxis axis in Enum.GetValues(typeof(SituationAxis)))
                    pressureDict[axis] = pressureModel.GetAxis(axis);
            }

            var factionDict = new Dictionary<FactionType, float>();
            var factionSystem = ServiceResolver.OptionalService<IFactionSystem>();
            if (factionSystem != null)
            {
                foreach (FactionType type in Enum.GetValues(typeof(FactionType)))
                {
                    var data = factionSystem.GetFaction(type);
                    if (data != null) factionDict[type] = data.Influence;
                }
            }

            var behaviorDict = new Dictionary<BehaviorType, int>();
            var ledger = ServiceResolver.OptionalService<IWorkSessionLedger>();
            if (ledger != null)
            {
                var snapshots = ledger.GetAllLastSessions();
                foreach (var kvp in snapshots)
                {
                    if (kvp.Value == null || string.IsNullOrEmpty(kvp.Value.ChoreType)) continue;
                    var behavior = NPCFourDimensionIntegration.MapChoreToBehavior(kvp.Value.ChoreType);
                    if (behavior.HasValue)
                    {
                        if (!behaviorDict.TryGetValue(behavior.Value, out var c)) c = 0;
                        behaviorDict[behavior.Value] = c + 1;
                    }
                }
            }

            var tagWeights = SocialTreeDirectionResolver.ComputeWeights(pressureDict, factionDict, behaviorDict, _config);

            return candidates
                .Select(node =>
                {
                    bool isRushedRemed = IsRushed(node.Id) && !IsRemediationComplete(node.Id);
                    float cost = _pool.ComputeNodeCost(node.Cost, node.Tier, _unlocks.UnlockedResearch.Count);
                    float gap = (1f - GetProgress(node.Id)) / cost; // 缺口进度比
                    float weight = gap * tagWeights[node.SocialTag];
                    if (isRushedRemed) weight *= _config.RemediationFavor;
                    return (node, isRushedRemed, weight);
                })
                .OrderByDescending(t => t.isRushedRemed) // 补课优先
                .ThenByDescending(t => t.weight)
                .Select(t => (t.node, t.isRushedRemed))
                .ToList();
        }

        /// <summary>保底注入：给缺口进度比最高的候选注入 0.1 进度（防死锁，不抢占交互）。</summary>
        public void InjectMinimumToStalled()
        {
            if (_db == null) return;
            var candidates = GetCandidateNodes();
            if (candidates.Count == 0) return;
            var target = candidates
                .Select(n => (node: n, p: GetProgress(n.Id)))
                .OrderBy(t => t.p)
                .First();
            AddProgress(target.node.Id, 0.1f);
            ModLogger.Debug($"[SocialTree] Stall fallback: injected 0.1 to {target.node.Id}");
        }

        public string SaveId => "SocialTreeProgress";

        public void Save(Dictionary<string, object> data)
        {
            data["Progress"] = new Dictionary<string, float>(_progress);
            data["Remediation"] = new Dictionary<string, float>(_remediation);
            data["Rushed"] = new List<string>(_rushed);
            data["LockedDirections"] = new List<int>(_lockedDirections.Select(t => (int)t));
        }

        public void Load(Dictionary<string, object> data)
        {
            _progress.Clear();
            _remediation.Clear();
            _rushed.Clear();
            _lockedDirections.Clear();

            if (data.TryGetValue("Progress", out var po) && po is Dictionary<string, float> progress)
                foreach (var kvp in progress) _progress[kvp.Key] = kvp.Value;

            if (data.TryGetValue("Remediation", out var ro) && ro is Dictionary<string, float> rem)
                foreach (var kvp in rem) _remediation[kvp.Key] = kvp.Value;

            if (data.TryGetValue("Rushed", out var ru) && ru is List<string> rushed)
                foreach (var id in rushed) _rushed.Add(id);

            if (data.TryGetValue("LockedDirections", out var lo) && lo is List<int> locked)
                foreach (var tagInt in locked) _lockedDirections.Add((SocialTag)tagInt);
        }

        public void Reset()
        {
            _progress.Clear();
            _remediation.Clear();
            _rushed.Clear();
            _lockedDirections.Clear();
        }
    }
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeProgress"
```
预期：所有测试 PASS。（`AllocateProgress_WithBalance_MaturesEligibleNode` 需要 `SocialTreeConfig.Initialize()` 时无 IConfigManager → 回落默认值，属预期。）

- [ ] **步骤 5：Commit**

```bash
git add src/Content/SocialResearch/SocialTreeProgress.cs ONIModPack.Tests/SocialTreeProgressTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "feat(social-tree): add SocialTreeProgress with tests"
```

---

### 任务 8：修改 SocialResearchUnlocks 加入 InterveneNode / IsRushed / ForceUnlock / 冷却

**文件：**
- 修改：`src/Content/SocialResearch/SocialResearchUnlocks.cs`
- 创建测试：`ONIModPack.Tests/SocialTreeInterveneTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写失败的测试**

创建 `ONIModPack.Tests/SocialTreeInterveneTests.cs`:

```csharp
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeInterveneTests
    {
        private static void Setup(bool enablePlayer, bool allowSkip)
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig
            {
                EnablePlayerLit = enablePlayer,
                AllowSkipPrereqLight = allowSkip
            };
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);

            var db = new SocialResearchDatabase();
            db.Initialize();
            ServiceRegistry.Register<SocialResearchDatabase>(db);

            var tree = new SocialResearchTree();
            tree.Initialize();
            ServiceRegistry.Register<SocialResearchTree>(tree);

            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            ServiceRegistry.Register<SocialDevelopmentPool>(pool);

            var progress = new SocialTreeProgress();
            progress.Initialize();
            ServiceRegistry.Register<SocialTreeProgress>(progress);

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            ServiceRegistry.Register<SocialResearchUnlocks>(unlocks);
        }

        [Fact]
        public void PlayerIntervene_Disabled_Rejected()
        {
            Setup(enablePlayer: false, allowSkip: true);
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("SocialObservation", false));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SkipPrereq_NotAllowed_Rejected()
        {
            Setup(enablePlayer: true, allowSkip: false);
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("MassPsychology", skipPrereq: true));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void InsufficientBalance_Rejected()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(100f); // < 500
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("SocialObservation", false));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ValidIntervene_WithoutSkip_UnlocksClean()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(1000f);
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();

            bool ok = unlocks.InterveneNode("SocialObservation", false);

            Assert.True(ok);
            Assert.True(unlocks.IsResearchUnlocked("SocialObservation"));
            Assert.False(unlocks.IsRushed("SocialObservation"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void InterveneWithSkipPrereq_MarksRushed()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();

            // MassPsychology 依赖 SocialObservation（未解锁）→ 跳过前置点亮
            bool ok = unlocks.InterveneNode("MassPsychology", skipPrereq: true);

            Assert.True(ok);
            Assert.True(unlocks.IsResearchUnlocked("MassPsychology"));
            Assert.True(unlocks.IsRushed("MassPsychology"));
            Assert.True(ServiceResolver.Get<SocialTreeProgress>().IsRushed("MassPsychology"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void DoesNotSkipPrereq_DependencyCheckBlocks()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.Get<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.Get<SocialResearchUnlocks>();

            // 不跳过前置时，依赖未满足 → 拒绝
            Assert.False(unlocks.InterveneNode("MassPsychology", skipPrereq: false));
            ServiceRegistry.Clear();
        }
    }
}
```

在 `ONIModPack.Tests/ONIModPack.Tests.csproj` 追加：
```xml
    <Compile Include="SocialTreeInterveneTests.cs" />
```

- [ ] **步骤 2：运行测试验证失败**

预期：FAIL，`InterveneNode`/`IsRushed` 不存在。

- [ ] **步骤 3：修改代码**

修改 `src/Content/SocialResearch/SocialResearchUnlocks.cs`：

（a）在文件头 `using ONIModPack.Core.Services;` 之后新增：
```csharp
using ONIModPack.Core.Compat;
```

（b）类中新增字段（`_unlockedBuildings` 声明之后）：
```csharp
        private int _lastInterveneCycle = -100; // Intervene 冷却计数（GameClock 周期）
```

（c）将现有 `UnlockResearch` 方法体（第 71~116 行）重构为"依赖校验 + 内部解锁"两层，并新增 `ForceUnlock`：

原 `UnlockResearch` 主体替换为：
```csharp
        public bool UnlockResearch(string researchId)
        {
            var db = GetDatabase();
            var node = db?.GetNode(researchId);
            if (node == null)
            {
                ONIModPack.Core.Logger.Warning($"[SocialResearchUnlocks] Cannot unlock unknown research: {researchId}");
                return false;
            }
            if (node.IsUnlocked)
            {
                ONIModPack.Core.Logger.Debug($"[SocialResearchUnlocks] Research already unlocked: {researchId}");
                return false;
            }

            var tree = GetTree();
            if (!(tree?.CanResearch(researchId, _unlockedResearch) ?? false))
            {
                ONIModPack.Core.Logger.Warning($"[SocialResearchUnlocks] Cannot unlock research {researchId}: dependencies not met");
                return false;
            }

            return UnlockNodeInternal(node);
        }

        /// <summary>
        /// 跳过依赖校验直接点亮（InterveneNode skipPrereq 路径用）。
        /// </summary>
        public bool ForceUnlock(string researchId)
        {
            var db = GetDatabase();
            var node = db?.GetNode(researchId);
            if (node == null || node.IsUnlocked) return false;
            return UnlockNodeInternal(node);
        }

        private bool UnlockNodeInternal(SocialResearchNode node)
        {
            node.IsUnlocked = true;
            _unlockedResearch.Add(node.Id);

            foreach (var buildingId in node.UnlockedBuildings)
            {
                UnlockBuilding(buildingId);
            }

            ONIModPack.Core.Logger.Info($"[SocialResearchUnlocks] Unlocked research: {node.Name}");

            EventBus.Publish(new SocialResearchUnlockedEvent
            {
                ResearchId = node.Id,
                ResearchName = node.Name,
                UnlockedBuildings = node.UnlockedBuildings
            });

            return true;
        }
```

（d）类末尾（`Reset` 之后）新增 API：

```csharp
        // ---- v0.92 SocialTree v2: Intervene 点亮 API ----

        private SocialTreeConfig GetSocialTreeConfig() => ServiceResolver.OptionalService<SocialTreeConfig>();
        private SocialDevelopmentPool GetDevelopmentPool() => ServiceResolver.OptionalService<SocialDevelopmentPool>();
        private SocialTreeProgress GetTreeProgress() => ServiceResolver.OptionalService<SocialTreeProgress>();

        /// <summary>
        /// 玩家主动干预点亮节点。
        /// </summary>
        /// <param name="nodeId">节点 ID</param>
        /// <param name="skipPrereq">是否跳过前置依赖点亮（产生 Rushed 社会代价）</param>
        /// <returns>是否点亮成功</returns>
        public bool InterveneNode(string nodeId, bool skipPrereq = false)
        {
            var config = GetSocialTreeConfig();
            var pool = GetDevelopmentPool();
            var progress = GetTreeProgress();
            var db = GetDatabase();
            var tree = GetTree();

            if (config == null || pool == null || progress == null || db == null || tree == null)
            {
                ONIModPack.Core.Logger.Warning("[SocialResearchUnlocks] InterveneNode: missing services");
                return false;
            }

            // 1. 总开关
            if (!config.EnablePlayerLit)
            {
                ONIModPack.Core.Logger.Debug("[SocialResearchUnlocks] InterveneNode: player lit disabled");
                return false;
            }

            // 2. 冷却（GameClock 周期）
            var adapter = ServiceResolver.OptionalService<IGameAdapter>();
            int currentCycle = adapter != null ? adapter.CurrentCycle : 0;
            if (config.InterveneCooldownCycles > 0 &&
                currentCycle - _lastInterveneCycle < config.InterveneCooldownCycles)
            {
                ONIModPack.Core.Logger.Debug("[SocialResearchUnlocks] InterveneNode: on cooldown");
                return false;
            }

            // 3. 节点校验
            var node = db.GetNode(nodeId);
            if (node == null || node.IsUnlocked) return false;

            // 4. 依赖校验
            if (!skipPrereq)
            {
                if (!tree.CanResearch(nodeId, _unlockedResearch)) return false;
            }
            else if (!config.AllowSkipPrereqLight)
            {
                ONIModPack.Core.Logger.Debug("[SocialResearchUnlocks] InterveneNode: skip prereq not allowed");
                return false;
            }

            // 5. 成本与扣款
            float cost = pool.ComputeNodeCost(node.Cost, node.Tier, _unlockedResearch.Count);
            if (skipPrereq)
                cost *= config.SkipPrereqCostMult;
            else
                cost *= config.InterveneCostMult;

            if (!pool.TrySpend(cost))
            {
                ONIModPack.Core.Logger.Debug($"[SocialResearchUnlocks] InterveneNode: balance insufficient (need {cost:F0})");
                return false;
            }

            // 6. 解锁（跳过前置走 ForceUnlock 规避依赖校验）
            bool unlocked = skipPrereq ? ForceUnlock(nodeId) : UnlockResearch(nodeId);
            if (!unlocked)
            {
                pool.AddBalance(cost); // 回滚
                return false;
            }

            // 7. Rushed 标记 + 广播
            if (skipPrereq)
            {
                progress.MarkRushed(nodeId);
                EventBus.Publish(new RushedResearchEvent
                {
                    NodeId = nodeId,
                    NodeName = node.Name,
                    Tag = node.SocialTag
                });
                ONIModPack.Core.Logger.Info($"[SocialResearchUnlocks] InterveneNode: {node.Name} unlocked (Rushed)");
            }
            else
            {
                ONIModPack.Core.Logger.Info($"[SocialResearchUnlocks] InterveneNode: {node.Name} unlocked");
            }

            // 8. 记录冷却周期
            _lastInterveneCycle = currentCycle;

            return true;
        }

        /// <summary>
        /// 查询节点是否为 Rushed（跳过前置点亮；补课完成前持续为真）。
        /// 供消费方（v0.93 建筑折扣等）查询。
        /// </summary>
        public bool IsRushed(string nodeId)
        {
            var progress = GetTreeProgress();
            return progress != null && progress.IsRushed(nodeId);
        }
```

（e）`Save` 追加冷却字段：
```csharp
            data["LastInterveneCycle"] = _lastInterveneCycle;
```

（f）`Load` 追加冷却恢复（在 `UnlockedBuildings` 处理之后）：
```csharp
            if (data.TryGetValue("LastInterveneCycle", out var cycleObj) && cycleObj is int cycle)
            {
                _lastInterveneCycle = cycle;
            }
            else
            {
                _lastInterveneCycle = -100;
            }
```

（g）`Reset` 追加重置：
```csharp
            _lastInterveneCycle = -100;
```

- [ ] **步骤 4：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeIntervene"
```
预期：所有测试 PASS。

- [ ] **步骤 5：Commit**

```bash
git add src/Content/SocialResearch/SocialResearchUnlocks.cs ONIModPack.Tests/SocialTreeInterveneTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "feat(social-tree): add InterveneNode/IsRushed/ForceUnlock to SocialResearchUnlocks"
```

---

### 任务 9：修改 SocialResearchModule 注册新服务并改造注入逻辑 + Rushed 副作用

**文件：**
- 修改：`src/Content/SocialResearch/SocialResearchModule.cs`

- [ ] **步骤 1：修改代码**

（a）文件头追加 usings（现有 using 之后）：
```csharp
using UnityEngine;
using ONIModPack.Core.Compat;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;
```

（b）类中新增字段（`_lastKnownUnlockCount` 附近）：
```csharp
        private int _lastRushedCycle = -1; // Rushed 周期事件上次处理周期
```

（c）`Initialize` 末尾追加注册（第 78 行之前）：
```csharp
            var config = new SocialTreeConfig();
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);

            var developmentPool = new SocialDevelopmentPool();
            developmentPool.Initialize();
            ServiceRegistry.Register<SocialDevelopmentPool>(developmentPool);
            RegisterSaveable(developmentPool);

            var progressSystem = new SocialTreeProgress();
            progressSystem.Initialize();
            ServiceRegistry.Register<SocialTreeProgress>(progressSystem);
            RegisterSaveable(progressSystem);
```

（d）`Start` 追加主 tick 注册：
```csharp
                // 主 tick：SDP 产出 → NPC 分配 → Rushed 代价（10s 间隔）
                scheduler.RegisterSystem("SocialTreeDevelopment", OnSocialTreeTick,
                    SimulationPhase.Behavior, 300, 10f);
```

（e）`OnResearchCompletedPostfix` 语义改为"研究完成 → 池子注入 ResearchBoost"：
```csharp
        private static void OnResearchCompletedPostfix(object __instance)
        {
            try
            {
                var pool = ServiceResolver.OptionalService<SocialDevelopmentPool>();
                var cfg = ServiceResolver.OptionalService<SocialTreeConfig>();
                if (pool == null || cfg == null) return;

                pool.AddBalance(cfg.ResearchBoost);
                ModLogger.Info($"[SocialResearch] Research completed — injected {cfg.ResearchBoost:F0} SDP");
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, "[SocialResearch] OnResearchCompletedPostfix failed");
            }
        }
```

（f）新增 `OnSocialTreeTick` 与 `ApplyRushedSideEffects`：
```csharp
        private void OnSocialTreeTick(float deltaTime)
        {
            try
            {
                var pool = ServiceResolver.OptionalService<SocialDevelopmentPool>();
                var progress = ServiceResolver.OptionalService<SocialTreeProgress>();
                if (pool == null || progress == null) return;

                int maxDupes = 0;
                try { maxDupes = Components.LiveMinions?.Count ?? 0; } catch { }

                var (pressureAvg, factionInfluence, behaviorActivity) = pool.GetSocialStateFactors();
                float rate = pool.ComputeCurrentRate(maxDupes, pressureAvg, factionInfluence, behaviorActivity);
                pool.AddBalance(rate * deltaTime);

                if (progress.AllocateProgress())
                {
                    _secondsSinceLastUnlock = 0f; // 重置停滞计时
                    _lastKnownUnlockCount = progress.TotalUnlockedCount();
                }

                ApplyRushedSideEffects(deltaTime);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, "[SocialResearch] OnSocialTreeTick failed");
            }
        }

        private void ApplyRushedSideEffects(float deltaTime)
        {
            var cfg = ServiceResolver.OptionalService<SocialTreeConfig>();
            var progress = ServiceResolver.OptionalService<SocialTreeProgress>();
            var pressure = ServiceResolver.OptionalService<PressureModel>();
            var opinion = ServiceResolver.OptionalService<IPublicOpinionSystem>();
            var faction = ServiceResolver.OptionalService<IFactionSystem>();
            var db = ServiceResolver.OptionalService<SocialResearchDatabase>();
            var adapter = ServiceResolver.OptionalService<IGameAdapter>();

            if (cfg == null || progress == null || db == null) return;

            int currentCycle = adapter != null ? adapter.CurrentCycle : 0;
            bool cycleEffectsDue = adapter != null && currentCycle > _lastRushedCycle;

            foreach (var kvp in db.ResearchNodes)
            {
                var node = kvp.Value;
                if (node.SocialTag == SocialTag.Mixed || !progress.IsRushed(node.Id)) continue;

                // ① 摩擦注入对应压力轴（每 tick）
                if (pressure != null)
                {
                    var axis = SocialTreeDirectionResolver.MapTagToAxis(node.SocialTag);
                    if (axis.HasValue)
                        pressure.AddAxis(axis.Value, cfg.RushedFrictionPerSec * deltaTime);
                }

                // ② 民望/派系（每 GameClock 周期一次）
                if (cycleEffectsDue && opinion != null && faction != null)
                {
                    var topic = SocialTreeDirectionResolver.MapTagToOpinionTopic(node.SocialTag);
                    if (topic.HasValue)
                        opinion.ModifyOpinion(topic.Value, -cfg.RushedOpinionDrop);

                    var target = SocialTreeDirectionResolver.MapTagToFaction(node.SocialTag);
                    if (target.HasValue)
                    {
                        var data = faction.GetFaction(target.Value);
                        if (data != null)
                        {
                            data.Popularity = Mathf.Clamp01(data.Popularity - cfg.RushedOpinionDrop);
                        }
                    }
                }
            }

            if (cycleEffectsDue) _lastRushedCycle = currentCycle;
        }
```

`progress.TotalUnlockedCount()` 需要在 `SocialTreeProgress` 中补充公开只读计数（见下一步 4）。

（g）`OnProgressionTick` 保底语义改造：两处直接解锁改为最小注入。

开局宽限段：
```csharp
                // 1) 开局保底：60s 后仍无研究解锁 → 向起步节点注入最小进度
                if (currentCount == 0 && _secondsSinceLastUnlock >= STARTUP_GRACE_SECONDS)
                {
                    var progress = ServiceResolver.OptionalService<SocialTreeProgress>();
                    if (progress != null && !unlocks.IsResearchUnlocked("SocialObservation"))
                    {
                        progress.InjectMinimumToStalled();
                        _secondsSinceLastUnlock = 0f;
                        ModLogger.Info("[SocialResearch] Startup grace — injected minimum progress to SocialObservation");
                    }
                    return;
                }
```

停滞超时段：
```csharp
                // 2) 停滞保底：连续 5 分钟无新解锁 → 最小注入防死锁
                if (_secondsSinceLastUnlock >= STALL_TIMEOUT_SECONDS)
                {
                    var progress = ServiceResolver.OptionalService<SocialTreeProgress>();
                    if (progress != null)
                    {
                        progress.InjectMinimumToStalled();
                        _secondsSinceLastUnlock = 0f;
                        ModLogger.Info("[SocialResearch] Stall timeout — minimum progress injected");
                    }
                }
```

（h）`Shutdown` 末尾追加注销：
```csharp
            ServiceRegistry.Unregister<SocialTreeConfig>();
            ServiceRegistry.Unregister<SocialDevelopmentPool>();
            ServiceRegistry.Unregister<SocialTreeProgress>();
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build ONIModPack.csproj -c Debug`
预期：0 错误（若报 `TotalUnlockedCount` 缺失，先完成步骤 4 再构建）。

- [ ] **步骤 3：Commit**

```bash
git add src/Content/SocialResearch/SocialResearchModule.cs
git commit -m "refactor(social-tree): wire new services, SDP injection and Rushed side effects in module"
```

- [ ] **步骤 4：补充 SocialTreeProgress 只读计数**

在 `src/Content/SocialResearch/SocialTreeProgress.cs` 的 `LockedDirections` 属性附近追加：
```csharp
        public int TotalUnlockedCount()
            => _unlocks == null ? 0 : _unlocks.UnlockedResearch.Count;
```

再运行一次构建：`dotnet build ONIModPack.csproj -c Debug` → 0 错误。

---

### 任务 10：Rushed 映射测试（复用解析器）与全量回归

**文件：**
- 创建：`ONIModPack.Tests/SocialTreeRushedTests.cs`
- 修改：`ONIModPack.Tests/ONIModPack.Tests.csproj`

- [ ] **步骤 1：编写测试**

创建 `ONIModPack.Tests/SocialTreeRushedTests.cs`:

```csharp
using Xunit;
using ONIModPack.Content.SocialResearch;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Tests
{
    /// <summary>
    /// Rushed 社会代价链的映射表与补课摘除行为（副作用落地经模块 tick，由映射表 + 进度状态保证）。
    /// </summary>
    public class SocialTreeRushedTests
    {
        [Fact]
        public void TagToAxis_Coverage()
        {
            Assert.Equal(SituationAxis.IdeologicalSplit, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Culture));
            Assert.Equal(SituationAxis.LaborTension, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Labor));
            Assert.Equal(SituationAxis.SocialUnrest, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Economy));
            Assert.Equal(SituationAxis.MoralFatigue, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Mixed));
        }

        [Fact]
        public void TagToOpinionTopic_Coverage()
        {
            Assert.Equal(OpinionTopic.Religion, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Culture));
            Assert.Equal(OpinionTopic.WorkConditions, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Labor));
            Assert.Equal(OpinionTopic.Economy, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Economy));
            Assert.Equal(OpinionTopic.Welfare, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Mixed));
        }

        [Fact]
        public void TagToFaction_Coverage()
        {
            Assert.Equal(FactionType.Belief, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Culture));
            Assert.Equal(FactionType.Union, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Labor));
            Assert.Equal(FactionType.Science, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Economy));
            Assert.Equal(FactionType.Engineering, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Mixed));
        }

        [Fact]
        public void RemediationThreshold_IsOne()
        {
            // 补课完成阈值恒为 1：防止未来改动误改
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.MarkRushed("x");
            Assert.True(progress.IsRushed("x"));
            progress.AddRemediation("x", 0.99f);
            Assert.True(progress.IsRushed("x"));
            progress.AddRemediation("x", 0.01f);
            Assert.False(progress.IsRushed("x"));
        }
    }
}
```

在 `ONIModPack.Tests/ONIModPack.Tests.csproj` 追加：
```xml
    <Compile Include="SocialTreeRushedTests.cs" />
```

- [ ] **步骤 2：运行测试验证通过**

运行：
```
dotnet build ONIModPack.csproj -c Debug
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTreeRushed"
```
预期：所有测试 PASS。

- [ ] **步骤 3：Commit**

```bash
git add ONIModPack.Tests/SocialTreeRushedTests.cs ONIModPack.Tests/ONIModPack.Tests.csproj
git commit -m "test(social-tree): add Rushed mapping coverage tests"
```

---

### 任务 11：全量回归验证

**文件：**
- 无代码变更（仅验证）

- [ ] **步骤 1：全量构建**

```
dotnet clean ONIModPack.csproj -c Debug
dotnet build ONIModPack.csproj -c Debug
```
预期：0 错误。

- [ ] **步骤 2：运行全部 SocialTree 相关测试**

```
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug --filter "FullyQualifiedName~SocialTree"
```
预期：6 个测试文件（Config/Pool/Resolver/Progress/Intervene/Rushed）全部 PASS。

- [ ] **步骤 3：运行全部测试确认无回归**

```
dotnet test ONIModPack.Tests/ONIModPack.Tests.csproj -c Debug
```
预期：原有 176+ 测试 + 新增测试全部 PASS，无新增失败。

---

## 自检清单

| 检查项 | 状态 |
|--------|------|
| 规格覆盖：配置节（§九）→任务3；SDP 池（§三）→任务5；方向解析（§八）→任务6；NPC 推进（§四）→任务7；Intervene/Rushed（§五/六）→任务8/9/10；模块接线（§十二）→任务9；存档（§十）→任务5/7 Save/Load | ✅ |
| 规格不做的：不重写 21 节点、不新建事件框架、不逐建筑打折（v0.93） | ✅ 全部规避 |
| 占位符扫描：无 TODO/待定/后续 | ✅ |
| 类型一致性：`SocialTag`（任务1定义）贯穿任务4~9；`MapChoreToBehavior` 提升为 `internal static`（任务2）供任务5/7；`SocialTreeDirectionResolver.MapTagTo*`（任务6）供任务9/10；`ForceUnlock`（任务8）供 Intervene 跳过前置 | ✅ |
| 测试依赖闭环：每个测试文件 setup 注册全部所需服务并 `ServiceRegistry.Clear()` | ✅ |
| UI（SocialTreeSideScreen） | 规格允许但属 v0.93 视觉工作，本计划未包含（接口 API 已就绪：`InterveneNode`/`IsRushed`/`LockedDirections`/进度查询） |

---

计划已完成并保存到 `docs/superpowers/plans/2026-08-28-social-tree-v2-bidirectional-evolution.md`。

## 两种执行方式：

**1. 子代理驱动（推荐）** - 每个任务调度一个新的子代理，任务间进行审查，快速迭代

**2. 内联执行** - 在当前会话中使用 executing-plans 执行任务，批量执行并设有检查点

选哪种方式？