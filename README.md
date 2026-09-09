# Planet Survival

基于 Unity 6.2（6000.2.9f1）的 2.5D 第三人称生存游戏原型框架。游戏设计来源见 `HLD/skeleton_arch.md`。

## 快速开始

1. 使用 Unity Hub 以 Unity 6000.2.9f1 打开仓库根目录。
2. 打开 `Assets/Game/Scenes/Bootstrap.unity` 并进入 Play Mode，或从 Build Settings 的第一个场景启动构建。
3. 在主菜单选择 **Start Game**；Gameplay 场景会根据默认配置生成栅格地形、玩家、相机、光照和游戏时钟。
4. 使用 WASD 或方向键移动，使用 E 交互；使用 Esc 暂停或继续游戏。

## 目录

- `Assets/Game/Scripts/Core`：与具体玩法解耦的游戏时间等基础能力。
- `Assets/Game/Scripts/Player`：玩家属性、移动和交互入口。
- `Assets/Game/Scripts/UI`：事件驱动的生存 HUD、交互提示及菜单界面。
- `Assets/Game/Scripts/World`：栅格地图数据、区块坐标、地形生成、大气模型与表现。
- `Assets/Game/Scripts/Bootstrap`：Bootstrap 与 Gameplay 场景的组合入口。
- `Assets/Game/Configuration`：地图等共享设计配置资产。
- `Assets/Game/Scenes`：正式的 Bootstrap、MainMenu 和 Gameplay 场景。
- `Assets/Game/Tests/EditMode`：纯逻辑与领域规则测试。

## 扩展约定

- 游戏时间归 `GameSessionState` 所有：场景的 `GameClock` 通过 `Bind(session.ConfigureTime(...))` 驱动同一个 `GameTimeModel`，因此进出着陆舱不会重置天数与救援倒计时。
- 需要跨场景推进的生产装置不使用逐帧计时，而是记录“完成时刻”（`WaterProcessor`、`HydroponicsSlot` 均以游戏天为时间戳），这样玩家外出时水仍在净化、作物仍在生长。
- 冰-水-食物闭环：地表采集冰块 → 货舱层水处理器过滤出饮用水注入着陆舱储水罐 → 生活层水培架消耗水与土豆种薯产出更多土豆 → 烤炉烹饪为热量。地表冰含高氯酸盐与尘土，必须经处理器过滤才能饮用，这也是水需要处理时间的原因。着陆舱初始储水只够约两天，水是这条链的瓶颈。
- 新的主动交互对象实现 `IInteractable`。
- 新的被动环境影响实现 `IEnvironmentalEffect`，由触发器或环境系统调用。
- 自然状态变化通过 `SurvivalDecay.Tick` 接收游戏时间，不直接依赖现实时间。
- 地下资源只进入数据层，等勘测或挖掘玩法明确后再增加对应系统。
- 地面资源按区块流式加载：`ChunkResourcePlanner` 只依据世界种子与区块坐标决定布局，`ResourceChunkStreamer` 负责按玩家位置加载和卸载区块，并记住已采集的节点。
- 资源密度在 `DefaultResourceSpawnSettings` 中按“每区块平均数量”配置，小于 1 的密度表示该资源只出现在部分区块；`_spawnClearanceRadius` 保证玩家出生点周围不生成节点。
- 正式地图应将 `GridTerrainView` 的逐格 GameObject 原型替换为 Chunk Mesh 或其他批处理实现。
