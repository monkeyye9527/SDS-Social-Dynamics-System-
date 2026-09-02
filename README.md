# Social Dynamics System (SDS) v3.0

缺氧 (Oxygen Not Included) 的社交动力学系统模组：复制人拥有信仰、人格、社交压力、派系与政治玩法（选举 / 立法 / 罢工），并深度集成进原生 UI。

## 项目结构

```
src/
├── Core/                       # 核心框架
│   ├── Entry/ModEntry.cs       # 入口（模块发现/配置覆盖/建筑注册协调）
│   ├── Modules/                # 模块生命周期、Kahn 拓扑排序、热重载
│   ├── Diagnostics/            # DebugSystem(F3)、DevCommandRegistry
│   ├── Services/               # ServiceRegistry / ServiceResolver
│   ├── Config/ConfigManager.cs # global.yaml 模块开关 + 配置热重载
│   ├── Events/EventBus.cs      # 事件总线（线程安全，GameEvent 带 DuplicantId）
│   ├── Rules/EffectPipeline.cs # 效果管线（Modifier SourceId/Target）
│   ├── Compat/BuildingApiAdapter.cs
│   ├── Saving / Simulation / Logging / Utils
├── Content/
│   ├── SocialDynamics/         # 社交动力学（政治玩法闭环主模块）
│   │   ├── Politics/           # 派系/选举/竞选纲领/政府应对（PoliticalGroup v0.92）
│   │   ├── Governance/         # 合法性、政府响应
│   │   ├── Emergence/          # 涌现行为
│   │   ├── Legislation/        # 立法（LawTemplate/LegislationManager/UI）
│   │   ├── SocialCore/ UI/ Events/ Components/ SDSRuntime/ Proficiency/
│   │   ├── SocialDynamicsModule.cs / StrikeSystem.cs / SocialState.cs
│   ├── BeliefSystem/           # 信仰（FaithPropagationGrid、BeliefSystemCore）
│   ├── Behavior/               # 四维行为（FourDimensionSystem、NPCFourDimensionIntegration）
│   ├── TraitSystem/            # 特质系统（35+特质）
│   ├── SocialResearch/         # 社会研究树（SocialResearchDatabase）
│   ├── Legislation/            # 立法模型/运行时（模板、状态机）
│   ├── ArtificialWorld/        # 人工世界（FurnitureRegistration）
│   ├── AversionSystem/         # 厌恶系统（Chore 偏置、强厌恶 Nudge）
│   ├── EnvironmentAdaptation/  # 环境适应性成长
│   ├── WorkSessionLedger/      # 工作会话账本（4D/Aversion/Adaptation 单一数据源）
│   ├── Polity/                 # 政体与经济（ColonyTreasury 财政池，Phase 2 政体状态机）
│   ├── Personality/            # 五维人格
│   ├── Buildings/              # 建筑注册唯一入口 BuildingPatches（按模块门控）
├── Balance/
│   └── HungerModule.cs         # [WIP] 默认禁用
└── QoL/
    ├── AutoSweeperRangeModule.cs   # SolidTransferArm 拾取半径扩展
    ├── OverlayImprovementsModule.cs
    └── SmartStorageModule.cs
```

> 设计文档见 `docs/superpowers/specs/`（政治术语冻结、PoliticalGroup×FactionAction 接线等）。

## 核心架构

### 事件驱动架构 (v3.0)

```
Personality → [EventBus] → Belief → [EventBus] → Stress → [EventBus] → Strike
```

所有子系统通过 `EventBus` 解耦，支持动态订阅/取消订阅。

### 代码质量优化

| 优化项 | 状态 | 说明 |
|--------|------|------|
| 事件总线线程安全 | ✅ | ConcurrentDictionary + lock |
| 日志缓冲写入 | ✅ | 5秒批量刷新，减少95% I/O |
| 配置类型安全 | ✅ | 枚举支持 + 错误日志 |
| 模块循环检测 | ✅ | Kahn算法拓扑排序 |
| 信仰系统缓存 | ✅ | 脏标记 + 组件缓存 |

## 构建说明

### Visual Studio
1. 打开 `ONIModPack.csproj`
2. 配置 `lib/` 目录下的游戏 DLL
3. 按 `Ctrl+Shift+B` 编译

### Python 脚本
```bash
# 开发构建
python tools/build/build.py --target all

# 发布构建
python tools/build/build.py --release --install

# 开发模式（监听变化）
python tools/build/build.py --watch
```

## 许可证

MIT License