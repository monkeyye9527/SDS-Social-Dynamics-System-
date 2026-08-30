# ONI Mod Pack - Integrated v3.0

整合优化后的 ONI (Oxygen Not Included) Mod 包，融合了多个版本的最佳特性。

## 项目结构

```
src/
├── Core/                    # 核心框架
│   ├── IModModule.cs        # 模块接口
│   ├── ModuleManager.cs     # 模块管理 (Kahn拓扑排序)
│   ├── ModEntry.cs          # 入口
│   ├── EventBus.cs          # 事件总线 (线程安全)
│   ├── Config/ConfigManager.cs  # 配置管理
│   └── Utils/{Logger.cs, YamlHelper.cs}
├── Content/
│   ├── Personality/         # 五维人格系统
│   │   └── PersonalityProfile.cs
│   ├── BeliefSystem/        # 信仰系统
│   │   ├── BeliefType.cs
│   │   ├── BeliefSystemCore.cs  # 含脏标记优化
│   │   └── BeliefTraitManager.cs
│   ├── TraitSystem/         # 特质系统 (35+特质)
│   │   ├── TraitManager.cs
│   │   ├── TraitEffectSystem.cs
│   │   └── TraitSystemModule.cs
│   ├── SocialDynamics/      # 社交动力学
│   │   ├── SocialDynamicsModule.cs
│   │   ├── StressDynamicsSystem.cs
│   │   ├── EntertainmentNeedSystem.cs
│   │   └── StrikeSystem.cs
│   └── Buildings/
│       └── SolarPanelT2Module.cs
├── Balance/
│   └── HungerModule.cs
└── QoL/
    ├── AutoSweeperRangeModule.cs
    ├── OverlayImprovementsModule.cs
    └── SmartStorageModule.cs
```

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