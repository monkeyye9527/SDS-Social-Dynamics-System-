# 社会树 v2 — 双向驱动的社会演化树设计（SDP 池 + Progress/Intervene/Rushed）

版本：v1.0
日期：2026-08-28
状态：已批准（待实现）
上游模块：Content.SocialResearch（隶属 v0.92 平衡调整窗口）

---

## 一、范围声明

把社会树从"玩家研究事件驱动的自动链"改造成"社会自然演化的双向驱动树"。

| 项 | 内容 |
|----|------|
| 允许 | 社会开发点池（SDP）；NPC 自然推进（Progress，综合加权方向）；玩家点亮（Intervene，可跳过前置带社会代价）；分支方向锁定；全部玩法配置开关；存档扩展；UI（复用 SideScreen 模式） |
| 禁止 | 不重写 21 个节点结构；不新建 Issue/Interest/Demand 类管线；不引入新事件框架 |
| 不做 | 解锁物折扣逐建筑接线完整化（v0.93 范围）；涌现问题生成概率挂钩 Rushed（可选，v0.93） |

**现状核实（2026-08-28）**：

1. 数据库实际含 **21 个节点**（Tier 1~5，Cost 500 → 6000），字段：Id / Name / Description / Cost / Tier / UnlockedBuildings / Unlocks / Dependencies / IsUnlocked。[SocialResearchDatabase.cs](../../../src/Content/SocialResearch/SocialResearchDatabase.cs)
2. 现有两条推进路径（[SocialResearchModule.cs](../../../src/Content/SocialResearch/SocialResearchModule.cs)）：
   - ResearchCenter 研究完成 Patch（`OnResearchCompletedPostfix` → `AdvancesProgress(1)`）；
   - 保底调度器（60s 起步宽限 + 300s 停滞超时，10s 周期）。
3. `SocialResearchUnlocks.AdvanceProgress` 自动解锁**成本最低**的可研究节点；`UnlockResearch` 校验依赖、交付建筑、广播 `SocialResearchUnlockedEvent`，并已实现存档（`SaveId="SocialResearch"`）。[SocialResearchUnlocks.cs](../../../src/Content/SocialResearch/SocialResearchUnlocks.cs)
4. `SocialResearchTree.BuildTierStructure` 是 5 行单节点**线性骨架**，与数据库真实分叉依赖图**脱节**——UI 布局必须以数据库依赖图为准，不依赖该骨架。
5. 消费方：BeliefSystemCore（`IdeologySystem` 门槛）、PluginUnlockService（立法插件技术门槛）、SocialReportManager。`IsBuildingUnlocked` 当前无运行时消费方（仅记账）。
6. 下游接口已核实：`SituationAxis{ LaborTension, SocialUnrest, IdeologicalSplit, MoralFatigue }`、`IPublicOpinionSystem.ModifyOpinion(OpinionTopic, float)`、`FactionType{ Union, Engineering, Belief, Science }`。

---

## 二、概念模型：Progress / Intervene / Rushed

| 概念 | 内部名 | 含义 | 完成方式 |
|------|--------|------|----------|
| NPC 自然演化 | `Progress` | 社会正在"形成"这个制度 | 进度攒到 100% → 成熟点亮 |
| 玩家主动点亮 | `Intervene` | 玩家强行投入社会资源提前催化 | 立即点亮 |
| 跳过前置点亮 | `Intervene + SkipPrerequisite` | 越级催化 | 立即点亮 + 产生 `Rushed`（社会代价） |

- 每个节点新增 `Progress ∈ [0,1]`。NPC 分配注入 → 满 1 → `UnlockResearch`（成熟点亮，交付沿用现有链路）。
- Intervene 直接 `UnlockResearch`（完整节点视为被植入完成）。
- **Rushed 消退唯一途径**：该节点"补课"——Rushed 节点仍接受 NPC 分配至 `Remediation = 1`，随即摘除 Rushed（见第六节）。

---

## 三、社会开发点池（SocialDevelopmentPool）

新 `IService`，`SaveId="SocialTreeDevelopmentPool"`，生产侧单点产出。

**产出速率**（每 tick，tick 间隔默认 10s）：

```
rate = BaseRate × f(人口) × f(社会状态)
f(人口)      = MaxDupes^PopExponent          // MaxDupes = 当前活跃复制人数（Components.LiveMinions.Count）
f(社会状态)  = 1 + SWPressure × 压力活跃度
                  + SWFaction × 派系影响力
                  + SWBehavior × 行为活跃度
```

- 压力活跃度 = 4 根压力轴归一化均值（PressureModel 只读通道）。
- 派系影响力 = 各 `FactionType` 民望按规模占比折算 0~1（IFactionSystem 只读通道）。
- 行为活跃度 = WorkSessionLedger 周期内工作会话时长归一化 0~1（缺省回退 0.5）。
- 信号源缺服务时对应项按 0 计（OptionalService 空安全）。

**支出侧**：NPC 每 tick 取出 `池子余额 × SpendFractionPerTick` 用于分配（默认 0.04，防一次清空）；玩家 Intervene 一次性扣足额。

**树总成本递增**：

```
nodeCost(node) = node.Cost × tierMul(node.Tier) × (1 + ExpansionPerNode × 已点亮总数)
```

默认：T1~T5 层级系数 1.0 / 1.15 / 1.35 / 1.6 / 1.9；`ExpansionPerNode = 0.06`。

---

## 四、NPC 自动推进（SocialTreeProgress）

新 `IService`，`SaveId="SocialTreeProgress"`，持有：节点 `Progress`、`Remediation`、Rushed 字典、方向锁定状态。调度 `SocialTreeDevelopment`（Behavior 相位，priority 300，10s）。

每 tick 流程：

1. **可取支出额** = 池子余额 × `SpendFractionPerTick`（`EnableNpcAutoProgress=false` 时跳过 2~3，池子只留玩家）。
2. **候选节点** = 依赖满足 && 未解锁 && 未被打入停滞（锁定分支模式下仅锁定 `SocialTag` 的候选）。
3. **分配顺序**（优先级从高到低）：
   - Rushed 补课节点（`Remediation += 支出 / nodeCost`，`RemediationFavor` 倍率）；
   - 锁定分支候选（方向锁定激活时独占）；
   - 常规分配：按方向加权 `ComputeWeights()` 采样 `SocialTag`，再在标签内取缺口进度比最高的候选节点。
4. **成熟点亮**：`Progress ≥ 1` → `UnlockResearch`（复用现有交付链路），并广播 `ResearchNodeMaturedEvent`。

**保底调度器改造**：保留起步宽限（60s 无解锁 → SocialObservation 起步）与停滞兜底（300s 无成熟 → 向最低成本候选注入最小 Progress），语义从"直接解锁"改为"最小注入"，防死锁且不再抢占交互。

---

## 五、玩家 Intervene（点亮）

API 归口 `SocialResearchUnlocks` 扩展（避免新建全集服务）：

```csharp
// 返回是否点亮成功
bool InterveneNode(string nodeId, bool skipPrereq = false);
```

规则：

1. `EnablePlayerLit=false` → 拒绝。
2. 冷却期内（`InterveneCooldownCycles`，默认 2 个 GameClock 周期）→ 拒绝。
3. 依赖校验：`skipPrereq=false` 走既有 `CanResearch`；`skipPrereq=true` 需 `AllowSkipPrereqLight=true`，并进入 Rushed 分支。
4. 成本：`池子扣款 = nodeCost × (skipPrereq ? SkipPrereqCostMult : InterveneCostMult)`；余额不足拒绝。
5. 成功 → `UnlockResearch(nodeId)`；若 skipPrereq 则写入 Rushed 状态，并广播 `RushedResearchEvent`（含 `ResearchId` / `SocialTag`）。

---

## 六、Rushed 社会代价链

Rushed 节点持续作用，直到补课完成摘除：

```
Intervene + SkipPrerequisite
        ↓
Rushed 标记（制度成熟度不足，直到 Remediation=1 才消退）
        ↓
① 执行效率下降：IsRushed(nodeId)=true → 该节点解锁物能力 ×(1 - RushedEfficiencyPenalty)
        ↓
② 社会摩擦增加：每 tick 向 SocialTag 对应压力轴注入 f(RushedNodeCount) × RushedFrictionPerSec × dt
        ↓
③ 派系反弹 / 民望变化：周期事件（每 GameClock 周期一次）→ 对应派系民望偏移 + 民望话题负面
        ↓
④（可选，v0.93）Rushed 堆积 → 涌现问题生成概率上升
```

- 查询口：`SocialResearchUnlocks.IsRushed(nodeId)`，供信仰传播/社会建筑产出/立法插件折扣消费方查询（v0.92 只提供查询口 + 摩擦/民望/派系副作用落地；逐建筑折扣接线排 v0.93）。
- 摩擦走 `PressureModel` 既有的轴输入通道（Tag→轴见第八节）；派系走 `IFactionSystem`；民望走 `IPublicOpinionSystem.ModifyOpinion`。
- **Tag→民望话题 / 派系映射（仿 LegislationMacroEffects，明确无二义）**：

  | SocialTag | 民望话题 | 派系偏移目标 |
  |-----------|----------|--------------|
  | Culture | Religion | Belief |
  | Labor | WorkConditions | Union |
  | Economy | Economy | Science |
  | Governance | Welfare | Engineering |
  | Mixed | 跳过民望（仅摩擦） | 各派系均摊 |

- 防抖：③ 仅每周期触发一次（实现用周期计数器）。

---

## 七、方向标签 SocialTag

`SocialResearchNode` 增字段 `SocialTag`（枚举 `Mixed / Culture / Labor / Economy / Governance`，默认 Mixed）。21 节点标注：

| 节点 | Tag | 节点 | Tag |
|------|-----|------|-----|
| SocialObservation | Mixed | PropagandaEngineering | Culture |
| MassPsychology | Culture | CulturalShaping | Culture |
| BasicPropaganda | Culture | IdeologySystem | Culture |
| SocialEngineering | Culture | GroupDynamics | Labor |
| OrganizationalTheory | Labor | UnionSystem | Labor |
| LaborManagement | Labor | WorkScheduling | Labor |
| Specialization | Economy | ColonyLaws | Governance |
| ResourceAllocation | Economy | PublicWelfare | Governance |
| Technocracy | Economy | ArtificialGovernance | Governance |
| ColonyParliament | Governance | SocialSimulation | Governance |
| ColonyGovernance | Governance | | |

---

## 八、信号 → 标签映射表（SocialTreeDirectionResolver，纯函数）

可测纯函数：`ComputeWeights(pressureSignals, factionSignals, behaviorSignals, config) → Dictionary<SocialTag, float>`

| 信号源 | 类别 → 标签 |
|--------|-------------|
| 压力轴 | LaborTension → Labor；IdeologicalSplit → Culture；MoralFatigue → Governance；SocialUnrest → Governance（骚乱指向治理） |
| 派系民望 | Union → Labor；Belief → Culture；Engineering → Governance；Science → Economy |
| 行为构成 | 劳动建造类 → Labor；艺术/学习类 → Culture；研究/专项 → Economy；社交/照护类 → Governance（行为类别以 `NPCFourDimensionIntegration.MapChoreToBehavior` / WorkSessionLedger 为准） |

三类信号各自归集后乘 `SWPressure / SWFaction / SWBehavior` 并规整（和=1）；全部为空 → 各标签等权（默认偏向 Mixed 前序）。

---

## 九、配置节 SocialTreeConfig

新 `IService`，沿用 `ReliefConfig` 模式：`MODULE="Content.SocialResearch"`，键前缀 `SocialTree.`，经 `IConfigManager` 外部覆盖，缺省回落代码默认值。

| 字段 | 默认值 | 单位 | 语义 |
|------|--------|------|------|
| `EnableNpcAutoProgress` | true | bool | NPC 自动推进总开关（关→池子只留玩家） |
| `EnablePlayerLit` | true | bool | 玩家点亮玩法总开关 |
| `AllowSkipPrereqLight` | true | bool | 允许跳过前置点亮 |
| `SkipPrereqCostMult` | 1.6 | 乘数 | 跳过前置时点亮成本乘子 |
| `InterveneCostMult` | 1.0 | 乘数 | 玩家点亮成本乘子（默认全价） |
| `InterveneCooldownCycles` | 2 | 周期 | 点亮冷却（0=关闭） |
| `DirectionControl` | "Lockable" | 枚举串 | `Auto` / `Lockable`（锁定分支交互可用性） |
| `BaseRate` | 0.05 | /s | SDP 基础产出速率 |
| `PopExponent` | 0.45 | 指数 | 人口因子幂 |
| `SWPressure` | 0.5 | 权重 | 方向加权中压力轴比例 |
| `SWFaction` | 0.3 | 权重 | 方向加权中派系民望比例 |
| `SWBehavior` | 0.2 | 权重 | 方向加权中行为构成比例 |
| `SpendFractionPerTick` | 0.04 | 比率 | NPC 每 tick 支出占池子比例 |
| `ExpansionPerNode` | 0.06 | 系数 | 每点亮一节点总成本增幅 |
| `TierMulT1..T5` | 1.0/1.15/1.35/1.6/1.9 | 乘数 | 层级成本系数 |
| `RushedEfficiencyPenalty` | 0.3 | 折扣 | Rushed 解锁物能力折扣（0.3=打七折） |
| `RushedFrictionPerSec` | 0.002 | /s | Rushed 摩擦注入速率（每 Rushed 节点） |
| `RushedOpinionDrop` | 0.002 | 一次性 | Rushed 周期事件民望下降量 |
| `RemediationFavor` | 2.0 | 倍率 | Rushed 补课优先分配倍率 |
| `ResearchBoost` | 200 | 一次性 | 玩家研究完成 → SDP 注入量 |

---

## 十、存档扩展（数据主权划分 + 旧档兼容）

| 数据 | 归属服务 | SaveId |
|------|----------|--------|
| 已解锁研究/建筑 | SocialResearchUnlocks（不变） | `SocialResearch` |
| SDP 池余额 | SocialDevelopmentPool（新增） | `SocialTreeDevelopmentPool` |
| 节点 Progress / Remediation / Rushed / 方向锁定 | SocialTreeProgress（新增） | `SocialTreeProgress` |

- 新服务在 `SocialResearchModule.Initialize` 中 `RegisterSaveable`。
- **旧档兼容**：三个键按缺省比例回落（余额 0、Progress 全 0、无 Rushed、无锁定），已解锁研究/建筑照常恢复；树从"已解锁基础"继续自然演化，不重置玩家进度。
- 加载时原子性：先恢复解锁集，再恢复 Progress 表；两表冲突（Progress≥1 但未解锁）以解锁集为准。

---

## 十一、UI（SocialTreeSideScreen）

- 复用现有 SideScreen 模式（参照 SocialDynamics/UI 既有屏幕），从 CommunityCenter 侧屏入口进入。
- 布局以**数据库依赖图为准**（不依赖已脱节的 `BuildTierStructure`），按 Tier 行 + 依赖连线排布。
- 节点卡片：名称、SocialTag 徽标、成本、`Progress` 进度条、Rushed 徽标、依赖/解锁箭头、状态。
- 交互：
  - **Intervene** 按钮（实时预览 SDP 消耗；余额不足置灰）；
  - 跳过前置复选（`AllowSkipPrereqLight=true` 时显示）；
  - **分支锁定**（`DirectionControl="Lockable"` 时，按 SocialTag 分组锁定按钮；锁定状态存存档）。

---

## 十二、模块接线改造（SocialResearchModule）

- `Initialize` 增注册：`SocialTreeConfig`、`SocialDevelopmentPool`、`SocialTreeProgress`（两个新 Saveable 各 `RegisterSaveable`）。
- `Start` 注册调度 `SocialTreeDevelopment`（Behavior 300 / 10s）：SDP 产出 → NPC 分配 → Rushed 代价 tick。
- `RegisterPatches` 保留 ResearchCenter Patch，语义改为**研究完成 → 池子注入 `ResearchBoost`**（知识溢出），不再直接推进链。
- 保底调度器保留（见第四节改造），优先级低于 NPC 分配。
- 遗留：`AdvanceProgress` 保留（供测试/调试），生产路径不再调用。
- 消费方（BeliefSystemCore / PluginUnlockService / SocialReportManager）**接口不变**；新增 `IsRushed` 查询口供 v0.93 折扣接线。

---

## 十三、测试计划

测试目录 `ONIModPack.Tests`，沿用 NUnit + 模拟时钟模式。用例：

| 测试文件 | 覆盖 |
|----------|------|
| SocialTreeConfigTests | 配置默认值加载、覆盖生效 |
| SocialDevelopmentPoolTests | 速率公式（人口/社会状态因子）、成本曲线（tier×扩展）、支出比例、余额不足 |
| SocialTreeDirectionResolverTests | 三类信号→标签权重纯函数、空信号回退等权、锁定分支覆盖 |
| SocialTreeProgressTests | 分配达 100% 成熟点亮并交付；池空停投；`EnableNpcAutoProgress=false` 不支出；保底最小注入 |
| InterveneTests | 全价点亮、依赖校验、`AllowSkipPrereqLight=false` 拒绝、跳过成本乘子、冷却、`EnablePlayerLit=false` 拒绝 |
| RushedTests | 摩擦注入映射轴、周期民望/派系副作用、补课完成后摘除、`IsRushed` 正确性 |
| MigrationTests | 旧档无新键回落加载、解锁集保留 |

**验证门槛**：`ONIModPack.Tests` 全量通过、构建 0 error；现有 176+ 测试不回归。

---

## 十四、接口变更摘要

- 新建：`SocialTreeConfig`、`SocialDevelopmentPool`、`SocialTreeProgress`、`SocialTreeDirectionResolver`（静态纯函数）、`ResearchNodeMaturedEvent`、`RushedResearchEvent`。
- 修改：`SocialResearchNode`（+`SocialTag`）、`SocialResearchUnlocks`（+`InterveneNode` / `IsRushed`）、`SocialResearchModule`（调度/注入改造）、`SocialResearchTree.BuildTierStructure` 弃用（UI 改走数据库依赖图，方法保留）。
- 保留：`AdvanceProgress`、`UnlockResearch`、档案键 `SocialResearch`、消费方接口。