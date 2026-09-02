# 美术资源目录结构

## 目录结构

```
assets/
├── buildings/              # 建筑美术资源
│   ├── social/          # 社交建筑
│   │   ├── community_center/
│   │   │   ├── sprites/
│   │   │   │   ├── main.png
│   │   │   │   ├── icon.png
│   │   │   │   └── slot_storage_0.png
│   │   │   ├── animations/
│   │   │   │   ├── community_center_idle.kanim
│   │   │   │   ├── community_center_working.kanim
│   │   │   │   └── community_center_build.kanim
│   │   │   └── configs/
│   │   │       └── slots.yaml
│   │   ├── union_hall/
│   │   ├── broadcast_station/
│   │   ├── think_tank/
│   │   ├── memorial_plaza/
│   │   ├── community_garden/
│   │   └── welfare_center/
│   └── ...
├── ui/                  # UI美术资源
│   ├── icons/
│   ├── panels/
│   └── overlays/
├── effects/             # 特效资源
│   ├── particles/
│   └── effects/
├── config/             # 配置文件
│   └── art_config.yaml
└── bundles/          # 资源包配置
```

## 建筑资源文件格式

### 精灵文件格式

#### 1. 建筑精灵配置 (slots.yaml)
```yaml
building_name: community_center
slots:
  - id: "storage_1"
    type: "storage"
    x: -1
    y: 0
    capacity: 200.0
    is_optional: false
    description: "Storage for community supplies"
  - id: "storage_2"
    type: "storage"
    x: 1
    y: 0
    capacity: 200.0
    is_optional: false
    description: "Storage for community supplies"
  - id: "work_1"
    type: "work"
    x: 0
    y: 1
    is_optional: false
    description: "Work station for community activities"
  - id: "interaction_1"
    type: "interaction"
    x: 0
    y: 0
    is_optional: false
    description: "Social interaction point"

tints:
  - name: "Wood"
    color: "#8B4513"
  - name: "Glass"
    color: "#ADD8E6"
```

#### 2. 动画配置
```yaml
building_name: community_center
animations:
  idle:
    frames: 8
    fps: 10
    loop: true
    file: "community_center_idle.kanim"
  working:
    frames: 16
    fps: 15
    loop: true
    file: "community_center_working.kanim"
  build:
    frames: 24
    fps: 20
    loop: false
    file: "community_center_build.kanim"
```

## 空位类型说明

| 类型 | 说明 | 组件 |
|------|------|------|
| storage | 存储空位 | Storage |
| work | 工作空位 | Workable |
| interaction | 交互空位 | BuildingInteractionComponent |
| power | 电力空位 | PowerConsumer |
| inlet | 输入空位 | ConduitConsumer |
| outlet | 输出空位 | ConduitDispenser |
| automation | 自动化空位 | LogicPorts |

## 资源命名规范

- 建筑主精灵: `{building_name}_main.png`
- 建筑图标: `{building_name}_icon.png`
- 空位精灵: `{building_name}_slot_{type}_{index}.png`
- 动画文件: `{building_name}_{anim_type}.kanim`
- 配置文件: `slots.yaml` / `animations.yaml`

## 坐标系统

使用相对坐标系统，以建筑中心为原点(0,0)：

```
      (-2, 2) (0, 2) (2, 2)
      (-2, 1) (0, 1) (2, 1)
(-1,-1)(0,-1)(1,-1)  建筑中心(0,0)
      (-2,-1)(0,-1)(2,-1)
      (-2,-2)(0,-2)(2,-2)
```

## Pivot 规范

| 精灵类型 | Pivot | 说明 |
|---------|-------|------|
| main | (0.5, 0.25) | 建筑主精灵，底部对齐 |
| icon | (0.5, 0.5) | 图标精灵，居中对齐 |
| slot | (0.5, 0.5) | 空位精灵，居中对齐 |

## 尺寸规范

| 资源类型 | 推荐尺寸 |
|---------|---------|
| 建筑主精灵 | 256x256 |
| 建筑图标 | 64x64 |
| 空位精灵 | 32x32 |
