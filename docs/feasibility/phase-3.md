# 阶段三可行性验证：地图与长期规划

## 1. 验证目标

阶段三关注的问题是:

- AI 能不能读地图和路径
- 能不能处理奖励、事件、商店、休息点等战斗外决策
- 能不能维护跨楼层、跨战斗的长期状态

## 2. 当前已确认的能力

已确认存在以下相关模块或类型:

- `MegaCrit.Sts2.Core.Map`
- `MegaCrit.Sts2.Core.Models.Acts`
- `MegaCrit.Sts2.Core.Entities.Rewards`
- `MegaCrit.Sts2.Core.Entities.Merchant`
- `MegaCrit.Sts2.Core.Entities.RestSite`
- `MegaCrit.Sts2.Core.Events`
- `MegaCrit.Sts2.Core.Commands.MapCmd`
- `Hook.AfterMapGenerated(...)`
- `Hook.BeforeRewardsOffered(...)`
- `Hook.AfterRewardTaken(...)`
- `Hook.BeforeRoomEntered(...)`
- `Hook.AfterRoomEntered(...)`

另外，已确认存在:

- `MegaCrit.Sts2.Core.AutoSlay`
- `MegaCrit.Sts2.Core.AutoSlay.Handlers.Rooms.CombatRoomHandler`
- `MegaCrit.Sts2.Core.AutoSlay.Handlers.Screens.MapScreenHandler`
- `MegaCrit.Sts2.Core.AutoSlay.Handlers.Screens.CardRewardScreenHandler`
- `MegaCrit.Sts2.Core.AutoSlay.Handlers.Screens.ChooseARelicScreenHandler`

这说明游戏内部至少存在某种自动处理房间和界面的结构。

## 3. 结论

阶段三当前判断为: `中等可行`

原因:

- 已确认地图、奖励、事件相关模块存在
- 已确认有地图生成和进房相关 Hook
- 已确认存在自动处理某些房间/界面的内部结构

但当前结论仍然保守，因为长期规划依赖的不只是 API 是否存在，还依赖这些对象是否容易被整理成稳定的全局状态。

## 4. 当前风险

阶段三的主要风险包括:

- 地图和房间对象可能跨多个模型层
- 奖励、商店、事件可能各自有不同的输入界面和选择方式
- 战斗外状态比战斗内状态更分散
- 长期规划需要将地图、资源、当前构筑、短期生存压力统一建模

## 5. 不反编译前的下一步

优先做下面这些验证:

1. 验证地图生成后能否读取节点与可走路径
2. 验证奖励出现时能否读到候选项
3. 验证休息点、商店、事件是否都有统一可观察入口
4. 先构建最小 `RunSnapshot`，只包含当前楼层、地图节点、金币、血量、遗物、牌组

## 6. 何时才需要反编译

只有在下面情况出现时，才对阶段三做定向反编译:

- 地图结构能拿到，但节点关系不清楚
- 奖励 / 事件 / 商店的实际选择入口藏在内部屏幕逻辑中
- `AutoSlay` 相关结构看起来可复用，但公开入口不足

反编译范围应限制在:

- 地图模型
- 奖励与房间选择相关类型
- `AutoSlay` 中与屏幕处理直接相关的少量类型

## 7. 当前推荐结论

阶段三不适合先开工实现，但适合在阶段一和阶段二建立稳定底座后，尽快做一次“地图 + 奖励”方向的最小可观察性验证。
