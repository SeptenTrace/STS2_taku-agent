# 阶段一可行性验证：读取对局状态

## 1. 验证目标

阶段一关注的问题是:

- 能不能读到当前战斗状态
- 能不能读到敌我单位状态
- 能不能读到手牌、牌堆、药水、遗物、buff/debuff
- 能不能在稳定时机采样，而不是只拿到偶发状态

## 2. 当前已确认的能力

已确认存在以下公开类型或入口:

- `MegaCrit.Sts2.Core.Combat.CombatManager`
- `MegaCrit.Sts2.Core.Combat.CombatState`
- `MegaCrit.Sts2.Core.Hooks.Hook`
- `MegaCrit.Sts2.Core.Entities.Players`
- `MegaCrit.Sts2.Core.Entities.Creatures`
- `MegaCrit.Sts2.Core.Entities.Cards`
- `MegaCrit.Sts2.Core.Entities.Potions`
- `MegaCrit.Sts2.Core.Entities.Relics`
- `MegaCrit.Sts2.Core.Entities.Powers`
- `MegaCrit.Sts2.Core.Entities.Intents`

已确认 `CombatState` 公开暴露:

- `RunState`
- `Allies`
- `Enemies`
- `Creatures`
- `Players`
- `RoundNumber`
- `CurrentSide`
- `Encounter`

这已经足够支撑最小战斗快照模型。

## 3. 当前已确认的采样时机

已确认 `Hook` 中存在大量生命周期回调，包括:

- `BeforeCombatStart`
- `AfterPlayerTurnStart`
- `BeforeTurnEnd`
- `AfterTurnEnd`
- `AfterCardDrawn`
- `BeforeCardPlayed`
- `AfterCardPlayed`
- `BeforePotionUsed`
- `AfterPotionUsed`
- `AfterRewardTaken`

这意味着阶段一不需要先研究底层线程或 UI 轮询，可以优先用 Hook 做事件驱动的快照采样。

## 4. 结论

阶段一当前判断为: `高可行`

原因:

- 已确认存在战斗状态中心对象
- 已确认存在多个稳定的战斗生命周期 Hook
- 已确认游戏实体按模块拆分，卡牌、遗物、药水、敌人和玩家并不是完全黑盒

## 5. 当前缺口

还没有完全确认的内容:

- 手牌、抽牌堆、弃牌堆、消耗区的具体字段路径
- 玩家 buff / debuff 的具体挂载结构
- 敌人意图对象的具体字段命名
- 某些 UI 上可见但底层是否直接可读的字段

这些缺口目前不构成阶段一不可行，只说明需要进一步做运行时读取和少量类型追踪。

## 6. 不反编译前的下一步

先做下面这些验证:

1. 在 mod 初始化阶段接入一个最小 Hook
2. 在 `AfterPlayerTurnStart` 或相近时机打印 `CombatState` 基础信息
3. 逐步确认 `Player`、`Creature`、`CardModel`、`PotionModel`、`RelicModel` 的可读字段
4. 先做最小快照，不急着一次覆盖所有状态

## 7. 何时才需要反编译

只有在下面情况出现时，才对阶段一做定向反编译:

- 已拿到 `Player` / `Creature` / `CardModel` 对象，但关键字段入口不明
- 运行时对象层级复杂，单靠公开成员找不到手牌或牌堆
- 意图、buff/debuff、费用变化等信息需要追踪内部实现

反编译范围应限制在:

- `CombatState`
- `Player`
- `Creature`
- `CardModel`
- `PotionModel`
- `RelicModel`
- 与战斗快照直接相关的少量 Reader 类型

## 8. 当前推荐结论

阶段一应该立即进入原型验证，不需要先做大规模反编译。

## 9. 运行时验证结果

当前仓库已经完成一轮实际运行验证，并成功导出战斗 JSON 快照。

已验证读取成功的内容:

- 玩家角色类型
- 玩家当前血量、最大血量、格挡、费用、最大费用、星数
- 玩家 buff / debuff 的标题、描述、层数、类别
- 敌方当前血量、最大血量、格挡、buff / debuff
- 敌方当前意图，包括意图类型、标签文本、状态机 `StateId`
- 我方手牌内容
- 抽牌堆、弃牌堆、消耗堆内容
- 我方药水标题与效果描述
- 我方遗物标题与效果描述
- 卡牌当前描述文本与解析后的费用信息

当前原型的采样时机:

- 战斗建立完成后
- 玩家回合开始后

当前原型的主要限制:

- 还没有做到“每张牌结算后”或“每次动作后”连续采样
- 战斗外状态、地图、奖励、商店、事件还不在阶段一当前原型范围内
- 现阶段重点仍然是保证战斗内快照稳定、字段含义明确、导出结果可复核

结论更新为:

- 阶段一不仅“可行”，而且已经有了可运行的第一版读取链路
- 在继续推进前，不需要做大规模反编译
- 后续反编译只需要在某些缺失字段或交互 API 不透明时做定向补充
