# SDS 政治玩法 v2 — 术语冻结 SPEC

版本：v0.90 P0  
日期：2026-08-18  
状态：已冻结（P0 阶段，P1–P3 实现期间不可改语义，仅可补充边界注释）

---

## 一、三类法律语义边界

### 1.1 SupremeLaw（至高法律）

| 维度 | 定义 |
|------|------|
| **创建者** | 玩家总督（唯一） |
| **权力来源** | 统治权 / 宪章 / 直接权威 |
| **审批流程** | 无需民主投票；`EnactSupremeLaw(lawId)` 直接 Draft → Enacted |
| **硬门槛** | LawValidator（参数/约束/冲突）+ Legitimacy.Value ≥ AuthorityCost |
| **典型内容** | 财产制度、劳动关系、税制、宗教自由、行政权限、组织自由 |
| **玩家意图** | "我设定制度边界，社会在边界内运行" |
| **生效语义** | 颁布即生效（SelfExecuting），不依赖执行机构 |
| **废止方式** | 玩家直接废止（RepealLaw），或通过新至高法覆盖（冲突互斥） |
| **子法关系** | 可被 PoliticalLaw 和 AdministrativeRule 的 `ParentLawId` 引用 |

**MVP 数据字段**（已落地在 `Legislation` 模型）：
- `Level = LawLevel.SupremeLaw`
- `AuthorityCost`（0..1，颁布时消耗，以 Legitimacy 为代理）
- `Scope`（描述性字符串，如 "全殖民地"）
- `EnforcementMode = EnforcementModel.SelfExecuting`（推荐默认值）
- `ParentLawId = null`（至高法为顶层）

### 1.2 PoliticalLaw（政治法律 / 涌现法律）

| 维度 | 定义 |
|------|------|
| **创建者** | NPC 政治组织 / 议会 / 地方机构（v0.91+ 涌现管线；组织由受影响群体聚合生成，见 v0.91 设计）；玩家也可提案 |
| **权力来源** | 代表性、组织力量、程序合法性 |
| **审批流程** | Propose → Debating → Voting → FinalValidation → Enacted/Rejected（现有管线） |
| **硬门槛** | LawValidator + 民望门槛（MinPopularSupport）+ 执政提案红利（RulingFactionService） |
| **典型内容** | 劳动法修正、安全条例、福利调整、税收方案、教育政策 |
| **玩家意图** | "NPC 因社会问题提出方案，我（或执政派系）决定是否通过" |
| **生效语义** | 经政治流程审批后生效；生效后通过 EffectPipeline 影响社会 |
| **废止方式** | 执政否决（VetoLaw，表决期）或 RepealLaw（生效后） |

**MVP 数据字段**（默认值，现有模板无需改动）：
- `Level = LawLevel.PoliticalLaw`
- `AuthorityCost = 0f`（不消耗权威）
- `EnforcementMode = EnforcementModel.AgencyRequired`（默认依赖行政机构）
- `ParentLawId`（可选，指向某至高法）

### 1.3 AdministrativeRule（行政规则）

| 维度 | 定义 |
|------|------|
| **创建者** | 地方机构 / 官员（v0.94+） |
| **权力来源** | 行政授权 |
| **审批流程** | 机构内部流程（轻量，未来实现） |
| **典型内容** | 工厂安全、矿井检查、医院流程、地方配给、机构管理 |
| **玩家意图** | "把法律落实到具体生产与生活，玩家通常不直接管理" |
| **生效语义** | 执行细节，不涉及宏观意识形态 |

**当前状态**：枚举已定义，模型已就绪，但无创建/审批流程实现。**v0.90–v0.93 期间不实现，仅数据层预留。**

---

## 二、Authority / Legitimacy / Enforcement 三值域定义

### 2.1 对照表

| 维度 | 回答的问题 | 当前实现 | 取值范围 | 主要读写者 |
|------|-----------|---------|---------|-----------|
| **Authority** | "我有没有权这么做？" | **暂未独立**（MVP 以 Legitimacy 为代理） | 0..1 | 玩家（颁布至高法时消耗） |
| **Legitimacy** | "大家认为这项统治是否正当？" | `Governance.Legitimacy`（`Value` 0..1） | 0..1 | GovernanceResponseSystem（Tick + ApplyWear + ApplyChange）、LegislationManager（AuthorityCost 代理） |
| **Enforcement** | "这条规则现实中执行得怎么样？" | **暂未独立**（MVP 以 `GovernanceResponseSystem.Budget` + `Legitimacy.Value` 为代理） | 0..1 | `EnforcementEngine`（未来）、`ConsequenceEngine`（承载能力调制） |

### 2.2 Authority（权威）

**语义**：统治者的制度权力上限。权威高 → 可以强行颁布高代价的至高法；权威低 → 即使合法性尚可，也无法推动激进改革。

**MVP 代理规则**：
- `AuthorityCost` 从 `Legitimacy.Value` 中扣除（`ApplyWear`）
- 当 `Legitimacy.Value < AuthorityCost` 时，`EnactSupremeLaw` 拒绝颁布
- **未来拆分**：Authority 独立为 `Governance.Authority`，与 Legitimacy 分轨计算

**读写清单**（拆分后，暂不实现）：
| 事件 | 方向 | 幅度 |
|------|------|------|
| 颁布至高法 | 消耗 | -AuthorityCost |
| 至高法被废止 | 部分恢复 | +AuthorityCost * 0.5 |
| 殖民地扩张 | 自然增长 | +0.001/s |
| 镇压反扑引爆 | 消耗 | -0.1 |

### 2.3 Legitimacy（合法性）

**语义**：民众对统治正当性的认可度。合法性高 → 治理效率高、涌现温度低、镇压反扑慢；合法性低 → 治理动作磨损加速、罢工/抗议更频繁。

**当前实现**：`Governance.Legitimacy`
- 放任衰减：`unrest > 0.9` 时 `-0.010/s`
- 治理回升：`unrest ≤ 0.9` 时 `+0.015/s`
- 磨损：`ApplyWear(amount)`（治理动作/镇压/否决）
- 增减：`ApplyChange(amount)`（立法通过/罢工谈判）
- 温度乘子：`GetTemperatureMultiplier()`（合法性高 → 乘子低 → 少爆雷）

**需要注意**（v0.90 已知问题）：当前 `Tick` 中 `unrest ≤ 0.9` 时自动上涨，导致"什么都不干合法性拉满，积极治理反而掉合法性"。**v0.91 修复建议**：改为只回升不自动涨，回升由治理成功事件触发（立法通过、罢工谈判成功、预算充足等）。

### 2.4 Enforcement（执行力）

**语义**：法律在现实中执行的程度。执行力高 → 法律违规率低、处罚有效；执行力低 → 法律形同虚设、违规泛滥。

**MVP 代理规则**：
- `EnforcementMode = SelfExecuting`：不依赖外部执行，直接生效
- `EnforcementMode = AgencyRequired`：依赖 `GovernanceResponseSystem.Budget × Legitimacy.Value` 作为执行效率代理
- `EnforcementMode = ResourceIntensive`：同上，但消耗双倍预算

**未来拆分**：`Governance.Enforcement` 独立值域，受行政机构规模、执法资源、腐败程度影响。

---

## 三、对象边界冻结

以下对象名称与语义在 v0.90–v0.93 期间**不可修改**（新增字段允许，但不可重命名或删字段）：

| 对象 | 命名空间 | 冻结范围 |
|------|---------|---------|
| `LawLevel` 枚举 | `Legislation.Model` | 三个值不可删、不可改名、不可调序 |
| `EnforcementModel` 枚举 | `Legislation.Model` | 三个值不可删、不可改名、不可调序 |
| `Legislation.Level` | `Legislation.Model` | 字段名不可改 |
| `Legislation.AuthorityCost` | `Legislation.Model` | 字段名不可改 |
| `Legislation.Scope` | `Legislation.Model` | 字段名不可改 |
| `Legislation.EnforcementMode` | `Legislation.Model` | 字段名不可改（注意：不是 `Enforcement`，已有同名字段） |
| `Legislation.ParentLawId` | `Legislation.Model` | 字段名不可改 |
| `LawTemplate.DefaultLevel` | `Legislation.Model` | 字段名不可改 |
| `IFactionEntity` | `Politics` | 7 个成员签名不可改 |
| `PoliticalGroup` | `Politics` | 10 个字段名不可改 |
| `FactionData.Goals` | `Politics` | 字段名不可改 |
| `FactionData.IssuePositions` | `Politics` | 字段名不可改 |
| `FactionData.Organization` | `Politics` | 字段名不可改 |

---

## 四、事件命名冻结

以下 EventBus 事件在 v0.90–v0.93 期间**不可改名**（新增字段允许）：

| 事件 | 命名空间 | 发布者 | 订阅者 |
|------|---------|--------|--------|
| `LawEnactedEvent` | `Legislation.Events` | `LegislationManager`（EnactSupremeLaw / FinalizeEnact） | `GovernanceModule`、`CampaignPlatformService` |
| `LawRepealedEvent` | `Legislation.Events` | `LegislationManager`（RepealLaw / ResolveConflictOnEnact） | `GovernanceModule` |
| `LawViolationEvent` | `Legislation.Events` | `RuleEvaluator` | `GovernanceModule` |
| `LawProposalStatusChangedEvent` | `Legislation.Events` | `LegislationManager` | UI |

---

## 五、开发阶段中不可跨越的语义边界

| 阶段 | 允许做 | 禁止做 |
|------|--------|--------|
| v0.90（当前） | 冻结术语、数据层扩展、至高法律 MVP | 新建 Issue/Interest/Demand 管线、改 FactionType 枚举 |
| v0.91 | 接入 EmergentProblem 管线、修复 Legitimacy 自动上涨问题、涌现法律首次生成（简化涌现管线：Propose → Debating 兼谈判 → Voting → Enacted/Rejected） | 删除 FactionData、重写 GetDuplicantFaction、新建 Issue/Interest/Demand 管线 |
| v0.92 | NPC 政治组织首次创建、PoliticalGroup 参与 FactionActionSystem 遍历 | 删除 IElectionSystem、删除 FactionType |
| v0.93 | EmergentLaw 接入 EffectPipeline、谈判深层化（多方诉求/联盟） | 删除旧 LawSystem 兼容代码（已不存在） |