# Community Center 后续完善 - 设计规格

## 1. 背景与目标

Community Center 建筑已实现基础功能（YAML 配置加载、社交互动），但 EventBus 接入不完整。本设计旨在：

- 完善 `PartyHeldEvent` 事件定义，添加位置和参与者信息
- BeliefSystem 订阅 `PartyHeldEvent`，派对成功后给予信念加成
- 创建集中式 `SocialEventListener`，处理忠诚度的双层影响（参与者全额 + 附近者距离衰减）

## 2. 架构设计

### 2.1 事件总线模式（混合模式）

| 系统 | 订阅方式 | 处理内容 |
|------|---------|---------|
| BeliefSystemCore | 独立订阅 | 信念加成（仅参与者） |
| SocialEventListener | 集中式订阅 | 忠诚度加成（双层影响） |

### 2.2 距离衰减公式

```
衰减系数 = max(0, 1 - 距离 / 辐射半径)
```

- 辐射半径：10 格（社区中心社交影响范围）
- 参与者：衰减系数 = 1（全额加成）
- 超出范围：衰减系数 = 0（无加成）

## 3. 核心改动

### 3.1 PartyHeldEvent 扩展

**文件**: `src/Content/Buildings/Social/CommunityCenterWorkable.cs`

新增字段：
- `BeliefBonus` - 信念加成值
- `BuildingCell` - 建筑所在格子
- `BuildingX`, `BuildingY` - 建筑位置
- `ParticipantIds` - 参与者 ID 列表
- `InfluenceRadius` - 辐射半径

### 3.2 BeliefSystemCore 订阅

**文件**: `src/Content/BeliefSystem/BeliefSystemCore.cs`

- `SubscribeToEvents()` 订阅 `PartyHeldEvent`
- `OnPartyHeld()` 处理信念加成逻辑
- `UnsubscribeFromEvents()` 取消订阅

### 3.3 SocialEventListener（新建）

**文件**: `src/Content/SocialDynamics/SocialEventListener.cs`

- 实现 `IModModule` 接口
- `Initialize()` 订阅 `PartyHeldEvent`
- `OnPartyHeldForLoyalty()` 处理忠诚度双层影响

### 3.4 CommunityCenterWorkable 更新

**文件**: `src/Content/Buildings/Social/CommunityCenterWorkable.cs`

- 添加 `_participantIds` 跟踪当前参与者
- 更新 `OnInteractionComplete()` 发布完整事件
- 添加 `GetNearbyDuplicants()` 辅助方法

### 3.5 注册 SocialEventListener

**文件**: `src/Content/SocialDynamics/SocialDynamicsModule.cs`

- 在 `Initialize()` 中注册 `SocialEventListener`

## 4. 数据流

```
CommunityCenterWorkable.InteractionComplete
        │
        ▼
EventBus.Publish(PartyHeldEvent)
        │
        ├─► BeliefSystemCore.OnPartyHeld()
        │       └─► AddBeliefStrength() (参与者)
        │
        └─► SocialEventListener.OnPartyHeldForLoyalty()
                ├─► 参与者: Loyalty += LoyaltyBonus
                └─► 附近者: Loyalty += LoyaltyBonus * 衰减系数
```

## 5. 测试计划

1. **YAML 配置测试** - 验证 slots.yaml 反序列化
2. **事件发布测试** - 验证 PartyHeldEvent 字段完整
3. **信念加成测试** - 验证参与者获得信念加成
4. **忠诚度双层影响测试** - 验证距离衰减公式

## 6. 规格自检

- [x] 无占位符或 TODO
- [x] 架构与功能描述一致
- [x] 范围聚焦（仅 Community Center 后续完善）
- [x] 需求明确（混合模式、双层影响）