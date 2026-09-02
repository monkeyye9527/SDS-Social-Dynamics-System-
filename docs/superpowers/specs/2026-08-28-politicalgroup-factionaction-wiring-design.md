# PoliticalGroup 参与 FactionActionSystem 遍历 — 设计 SPEC

版本：v0.92  
日期：2026-08-28  
状态：已批准（2026-08-28 用户确认）

---

## 一、背景

v0.90 冻结了政治术语与对象边界（`docs/superpowers/specs/2026-08-18-politics-v2-terminology.md`），并已落地：

- `PoliticalGroup`：动态政治组织（问题驱动、`IFactionEntity` 兼容、10 字段冻结，只可加字段）
- `FactionRepository.GetAllPoliticalEntities()`：FactionData + PoliticalGroup 统一遍历口（v0.90 已建，无人消费）
- `FactionActionSystem`（v0.75 政治反馈环）：仅遍历静态 `FactionType` 阵营，对法律 `FactionPopularity` 通道效果反应

v0.92 路线图要求：**PoliticalGroup 参与 FactionActionSystem 遍历**。
用户补充需求：**组织不仅被动应对归因法律，还会主动影响其他法律以扩大自身势力**。

当前缺口：[FactionActionSystem.cs](src/Content/SocialDynamics/Politics/FactionActionSystem.cs) 的 `EvaluateLawReaction` 全程使用 `FactionType`/`FactionData`，组织不进入循环；`EnumerateAllEntities` 遍历口闲置。

## 二、目标与禁止项（v0.92 禁线）

### 目标
1. 组织进入派系行动遍历，能以「归因 + 非归因」两条通道对法律表态
2. 组织行动可影响合法性/民望（与派系行动一致的闭环回写）
3. 组织通过持续行动扩张自身势力（Influence 收益）

### 禁止（来自冻结 SPEC）
- 不新建 Issue/Interest/Demand 管线
- 不改 `FactionType` 枚举、不删 `IElectionSystem`
- `IFactionEntity` 7 成员、`PoliticalGroup` 10 冻结字段签名不改（只可加字段）

## 三、核心设计：单一定位规则

对每条已生效法律 L 与每个组织 G，定义组织对法律的态度：

```
net(L, G) = Σ L.PassiveEffects[i].MagnitudePerSecond
            where Channel == LawEffectChannel.PressureAxis
              && Target  == nameof(G.SourceAxis)

归因权重：attributed(L, G) = (L.SponsorGroupId == G.Id) || (L.SourceProblemId == G.SourceIssue)
```

| net(L, G) | 语义 | 组织行为 |
|-----------|------|---------|
| < 0      | 缓解组织关切 | 声援（PublicEndorsement），归因时额外声望收益 |
| > 0      | 加剧组织关切 | 不满累积 → 施压（PoliticalPressure） |
| == 0     | 无关 | 无反应（不占预算） |

- **归因通道**（兑现初心）：归因法律加权 ×1.5，组织对"自己促成/催生的法律"情绪更强烈
- **扩权通道**（用户补充）：非归因但同域（对 G.SourceAxis 有 PressureAxis 效果）的法律同样触发——组织蹭成果/找借口，以此扩大势力

两条通道由同一条定位规则自然涌现，无需新术语。

## 四、数据模型变更（均在冻结允许范围）

### 4.1 `PoliticalGroup` 新增字段
```
public SituationAxis SourceAxis;   // 组织关切轴，创建时由工厂写入（永久绑定）
```
- [PoliticalGroupFactory.GetOrCreate](src/Content/SocialDynamics/Politics/PoliticalGroupFactory.cs) 创建时写入（`axis` 参数本就在方法签名中）
- 需 `using ONIModPack.Content.SocialDynamics.Emergence;`

### 4.2 `FactionAction` 新增字段（该记录类非冻结，可加字段）
```
public string GroupId;      // 组织行动时非空；派系/政府动作保持 null
public string ActorName;    // 行动者显示名（组织 Name / 派系枚举名）
```
- `Faction` 字段保留：派系动作沿用；组织动作时该语义弱化，以 `GroupId != null` 判别动作归属

## 五、遍历与判定改造（FactionActionSystem）

### 5.1 依赖
- 构造函数签名不变（`IFactionSystem` + `LegislationRegistry`）
- `PoliticalGroupFactory` 经 `ServiceResolver.OptionalService<PoliticalGroupFactory>()` 在 `Initialize()` 时解析（与 `EmergentLawGenerator` 同风格，便于测试桩注入）

### 5.2 `EvaluateLawReaction` 第二段（组织段）
```
pass 1 原 FactionData 路径（完全不动）
pass 2 组织路径：
  // 同一 effects 循环内聚合 netByAxis（PressureAxis 通道 → axis → 净速率）
  foreach group in _groups.Groups:
      G.SourceAxis 对应的 net = netByAxis[SituationAxis]（无则跳过）
      key = "g|" + G.Id + "|" + law.Id        // 前缀隔离，与派系 key 无冲突
      weight = attributed ? 1.5f : 1f
      net < 0 → 声援通道（grievance 消退 -0.01/s，与派系受益路径一致；门槛满足则 TryEndorseGroup）
      net > 0 → 不满累积：grievance += |net|*deltaTime*(0.3 + 0.7*G.Organization)*weight
                过阈值 → TryActGroup（施压）
```

### 5.3 复用结构
- `_grievance` / `_lastActionTime` / `_recentActions`：key 含 `g|` 前缀，与派系 `faction|lawId` 天然隔离
- 冷却沿用 `ActionCooldownSeconds = 60f`，阈值沿用 `ActionThreshold = 0.15f`

## 六、行动执行（共享泛化）

### 6.1 泛化
- `TryAct(FactionData, …)` / `TryEndorse(FactionData, …)` 抽取通用核心，接受 `IFactionEntity`（FactionData 与 PoliticalGroup 共用实参）

### 6.2 组织行动类型
- 仅 `PoliticalPressure` / `PublicEndorsement`（**不做** StrikeMobilization：独立组织无工会身份，ParentFaction 语义扩展属未来）

### 6.3 回写（与派系一致）
- 施压：`Legitimacy.ApplyWear(intensity * 0.02f)` + 民望下调 `-intensity * 0.03f`
- 声援：民望上调 `+intensity * 0.02f`

### 6.4 组织门槛（替换派系的 Popularity 门槛）
- 声援门槛：`G.Organization >= 0.3f`（成气候才发声）
- 施压门槛：沿用 grievance 阈值 + 冷却，不额外设限制

## 七、扩权收益（用户补充需求的落地）

组织每次成功行动（施压/声援）：
```
G.Influence = Clamp01(G.Influence + intensity * 0.01f * G.Organization)
```
归因法律成功声援的额外收益：
```
G.Popularity = Clamp01(G.Popularity + 0.005f)
```
→ 反复行动累积势力，形成"行动 → 扩权 → 更有行动资本"的正反馈。

## 八、测试计划

沿用现 FactionActionSystem 测试风格（注入桩注册表 + 纯逻辑断言）：

| # | 用例 | 断言 |
|---|------|------|
| 1 | 归因加剧法律 | 组织施压，`RecentActions` 记录 GroupId/ActorName 非空 |
| 2 | 归因缓解法律（Organization≥0.3） | 组织声援，Influence 微升 |
| 3 | 非归因同域法律 | 触发扩权通道（同样施压/声援） |
| 4 | 异轴无关法律 | 无反应，无行动记录 |
| 5 | 冷却 | 60s 内同 key 不重复触发 |
| 6 | 归因权重 | 归因 vs 非归因同幅度效果，归因更早触发（不满速率更快） |
| 7 | 旧路径回归 | 既有 FactionData 反应测试全部保绿 |

测试文件：`ONIModPack.Tests/.../FactionActionSystemTests.cs`（或同名新文件，以现有命名为准）。

## 九、范围界定（本轮不做）

- 组织解散/生命周期、联盟谈判 → v0.93（谈判深层化）一并做
- 政府应对的组织版（Concede/Suppress 对 GroupId）→ v0.93
- StrikeMobilization 对组织 → 待 ParentFaction 语义明确后扩展
- Issue/Interest/Demand 管线 → 冻结禁线，不做

## 十、风险与兼容性

| 风险 | 缓解 |
|------|------|
| 组织量增长 → Tick 开销 | 每组织仅处理存在 PressureAxis 效果的法律；组织量级远小于法律×组织笛卡尔积，且冷却限流 |
| 前缀 key 冲突 | `g|groupId|lawId` 与 `faction|lawId` 前缀互斥，无碰撞 |
| 冻结字段 | 只加字段不改签名；`SituationAxis` 为值类型，序列化兼容（Registry 存档已有 SourceProblemId 字段模式可参照） |
| 旧路径回归 | pass 1 逻辑零改动，既有测试保绿即验证 |