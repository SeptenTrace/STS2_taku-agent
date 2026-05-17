# 阶段一方案：构建 AI 可读的对局状态层

## 1. 阶段目标

阶段一的目标是把《杀戮尖塔 2》的当前对局信息整理成一个稳定、可验证、可序列化的数据层。

换句话说，这一阶段要解决的问题是:

- AI 如何知道现在是谁的回合
- AI 如何知道自己手里有什么牌
- AI 如何知道敌人的意图、血量、格挡、buff/debuff
- AI 如何知道自己的能量、药水、遗物、牌堆状态
- AI 如何拿到这些信息，并且确信这些信息是对的

这一阶段暂时不负责:

- 自动出牌
- 自动结束回合
- 地图路线规划
- 奖励选择

## 2. 阶段一的核心产物

阶段一结束时，仓库里应该至少具备下面四类能力:

### 2.1 战斗状态快照模型

定义一套结构化数据，用于表示当前战斗状态。

最低建议包含:

- `BattleSnapshot`
- `PlayerSnapshot`
- `EnemySnapshot`
- `CardSnapshot`
- `PileSnapshot`
- `PotionSnapshot`
- `RelicSnapshot`
- `StatusEffectSnapshot`

### 2.2 状态采样入口

在合适的游戏生命周期节点抓取状态。

最低建议支持:

- 战斗开始时采样
- 回合开始时采样
- 每次动作结算后采样
- 回合结束时采样
- 战斗结束时采样

### 2.3 调试输出能力

需要一个人类可读的输出机制，方便肉眼校验状态是否正确。

最低建议支持:

- 输出到游戏日志
- 输出 JSON 到本地文件
- 可按回合编号保存快照

### 2.4 一致性校验机制

需要一些基础自检，避免在错误状态上继续开发。

例如:

- 手牌数量是否等于卡牌列表长度
- 敌人数量是否和场上实体数量一致
- 玩家能量是否在合法范围
- 某些关键对象为 `null` 时是否跳过采样并记录原因

## 2.5 当前实现状态

仓库里已经落地了一个最小可用的阶段一实现，当前代码结构为:

```text
mod/src/
  Diagnostics/
    BattleStateCaptureService.cs
  Observation/
    ObservationApiCatalog.cs
    ObservationServer.cs
  Patches/
    BattleStateCapturePatches.cs
  State/
    Builders/
      BattleSnapshotBuilder.cs
      GameSnapshotBuilder.cs
    Exporters/
      SnapshotFileExporter.cs
    Snapshots/
      BattleSnapshot.cs
      GameSnapshot.cs
    Support/
      ObservationText.cs
      GodotNodeSearch.cs
    Validation/
      BattleSnapshotValidator.cs
```

当前已经能够导出的字段包括:

- `BattleSnapshot`
  - 时间戳
  - 触发器名称
  - 回合数
  - 当前行动方
  - 遭遇类型
- `PlayerSnapshot`
  - 角色类型
  - 名称
  - 当前 / 最大生命
  - 格挡
  - 当前 / 最大能量
  - 星数
  - 我方 buff / debuff
  - 药水
  - 遗物
  - 手牌、抽牌堆、弃牌堆、消耗堆
- `EnemySnapshot`
  - 名称
  - 模型类型
  - 当前 / 最大生命
  - 格挡
  - 是否存活
  - 是否可被攻击
  - 敌方 buff / debuff
  - 当前意图摘要
- `CardSnapshot`
  - 标题
  - 当前描述文本
  - 类型
  - 稀有度
  - 目标类型
  - 费用
  - 是否 X 费
  - 星费
  - 是否升级
  - 关键词
- `PotionSnapshot`
  - 标题
  - 描述
  - 使用方式
  - 目标类型
  - 稀有度
- `RelicSnapshot`
  - 标题
  - 描述
  - 计数器
  - 稀有度
- `StatusEffectSnapshot`
  - 标题
  - 描述
  - 层数 / 数值
  - 正负面类别

当前采样时机:

- `combat_setup`
- `after_player_turn_start`
- `after_card_played`

当前输出路径:

- `~/Library/Application Support/STS2TakuAgent/phase1-feasibility/`

当前本地观察入口:

- HTTP server: `http://localhost:15527/`
- CLI: `./sts`

这说明阶段一的“状态读取层”已经不是纸面规划，而是有运行时 JSON 快照产出的原型实现。

## 2.6 当前阶段一已完成事项

已经完成:

- 战斗状态快照模型
- 出牌后动作历史记录
- 面向调试的 JSON 快照导出
- 面向模型的 `GameSnapshot` 统一状态容器
- `context` 场景识别
- `compact observation` 紧凑观察层
- `delta observation` 增量观察层
- 通用 `actions` 动作面接口
- 当前上下文 `knowledge/current` 知识缓存
- 细粒度只读 API
- 本地 CLI 封装
- 语义化 `combat/actions`
- 地图 / 事件 / 商店 / 奖励 / 篝火 / 宝箱 / 选卡界面的统一动作面
- 完整低 token 查询流

当前战斗查询推荐路径:

1. `context`
2. `combat/summary`
3. `actions`
4. `combat/enemies`
5. 必要时再查 `knowledge/current`
6. 只有在还不够时才查 `combat/hand`

当前局外查询推荐路径:

1. `context`
2. 场景 summary endpoint
3. `actions`
4. `player/summary`
5. 必要时再查 `knowledge/current`

这意味着阶段一现在已经从“能读战斗”扩展成了“能读整局，并且默认查询成本受控”。

## 2.7 参考源与当前差异

`STS2MCP` 是当前非常有价值的参考源，尤其在下面几方面:

- Hook 覆盖范围
- 房间 / overlay 场景识别
- 运行时对象读取方式
- 各类 UI 和房间状态的拼装方法

但当前仓库的目标不只是复刻它的状态读取能力。

当前设计的重点差异是:

- 我们更强调低 token
- 我们更强调模型默认查询路径
- 我们更强调细粒度 endpoint 与 CLI 封装
- 我们不鼓励默认读取 `full state`

也就是说，`STS2MCP` 更像“完整游戏接口层”，而当前仓库阶段一要做的是“面向 LLM 的节流观察层”。

## 2.8 下一步

阶段一已经完成，下一步不再继续往观察层塞更多字段，而是进入阶段二动作层。

阶段二起点已经明确:

- 基于 `actions` 中已经稳定下来的动作名和参数结构，开始落可执行 API
- 用 `delta observation` 为动作执行后的回读提供最小反馈
- 让动作日志、动作结果、观测增量形成闭环
- 优先打通战斗动作，然后扩到地图、奖励、事件、商店和多步选择界面

## 3. 阶段一任务拆分

建议拆成 5 个子模块推进。

## 任务 A：先定位可读取的数据源

目标:

- 找到战斗、玩家、敌人、手牌、药水、遗物等对象在 STS2 运行时中的来源

产出:

- 一份内部笔记或代码注释，记录关键类、关键字段、关键生命周期

验收:

- 至少能明确“从哪里读玩家状态”“从哪里读敌人列表”“从哪里读当前手牌”

## 任务 B：定义统一快照模型

目标:

- 建立独立于游戏内部对象的快照结构

设计原则:

- 不直接把游戏对象暴露给 AI 层
- 命名稳定
- 字段语义清晰
- 易于 JSON 序列化
- 能兼容未来地图、事件、奖励等扩展

建议字段示例:

```text
BattleSnapshot
- encounterId
- floor
- turn
- phase
- player
- enemies
- hand
- drawPile
- discardPile
- exhaustPile
- potions
- relics
- availableEnergy
```

## 任务 C：实现快照构建器

目标:

- 把游戏运行时对象转换成快照对象

建议实现方式:

- 新建 `SnapshotBuilder` 或 `BattleSnapshotService`
- 每一类对象单独拆转换函数
- 对复杂字段加防御式判空

推荐拆分:

- `BuildBattleSnapshot()`
- `BuildPlayerSnapshot()`
- `BuildEnemySnapshot()`
- `BuildCardSnapshot()`
- `BuildPotionSnapshot()`
- `BuildRelicSnapshot()`

## 任务 D：实现调试导出

目标:

- 让你能快速检查“当前读到的数据是不是对的”

建议先做最小方案:

- 每到玩家回合开始时导出一份 JSON
- 文件名包含时间戳、战斗编号、回合数
- 日志里打印导出路径

建议目录:

- `debug_snapshots/`

建议输出内容:

- 完整 JSON
- 一份简短摘要，包含血量、能量、手牌、敌人意图

## 任务 E：建立最小验证回路

目标:

- 每做完一批字段，就能立即进游戏核对

建议验证顺序:

1. 玩家基础状态
2. 敌人基础状态
3. 手牌与能量
4. 药水与遗物
5. buff/debuff 与特殊状态

## 4. 建议的数据范围

阶段一不要一开始追求“把所有东西都读出来”。先围绕 AI 做出决策真正需要的信息收敛范围。

优先级 P0:

- 当前回合数
- 当前行动方
- 玩家生命、格挡、能量
- 手牌列表
- 敌人生命、格挡、意图

优先级 P1:

- 抽牌堆、弃牌堆、消耗区
- 玩家 buff / debuff
- 敌人 buff / debuff
- 药水
- 遗物

优先级 P2:

- 卡牌升级状态、费用变化、临时状态
- 意图的更细粒度参数
- 特殊战斗状态
- 战斗外状态入口

## 5. 建议的代码结构

可以在当前仓库基础上先补一个面向阶段一的目录布局:

```text
mod/src/
  ModEntry.cs
  State/
    Snapshots/
    Builders/
    Exporters/
    Validation/
  Runtime/
    Hooks/
    Readers/
```

建议职责:

- `Runtime/Hooks`: 监听游戏生命周期
- `Runtime/Readers`: 从游戏对象中取原始数据
- `State/Builders`: 构建标准快照
- `State/Exporters`: 写日志或导出 JSON
- `State/Validation`: 做字段一致性检查

## 6. 阶段一的接口设计建议

后续如果要接规则系统、LLM 或其他 agent，阶段一最好尽早统一接口。

建议保留两个输出形态:

### 6.1 完整快照

用途:

- 调试
- 回放
- 离线分析
- 训练数据积累

形式:

- JSON 文件
- 字段尽量完整

### 6.2 紧凑摘要

用途:

- 提供给推理模块
- 降低 token 或上下文成本

形式:

- 文本摘要
- 或者裁剪后的轻量 JSON

## 7. 阶段一的里程碑

### P1-1：最小战斗快照

完成内容:

- 玩家 HP / Block / Energy
- 敌人 HP / Intent
- 手牌名称与费用

验收:

- 能在一场普通战斗的回合开始导出快照

### P1-2：完整回合级快照

完成内容:

- 抽牌堆、弃牌堆、消耗区
- 药水、遗物
- buff / debuff

验收:

- 回合级导出信息足够让人类判断“这回合大概要怎么打”

### P1-3：稳定性与校验

完成内容:

- 判空与错误日志
- 快照一致性检查
- 多种战斗场景测试

验收:

- 常见战斗中导出稳定，没有大面积缺字段或错位

## 8. 阶段一完成定义

满足下面条件，可以认为阶段一完成:

- 能在战斗关键节点稳定抓到快照
- 快照覆盖 AI 决策所需的核心战斗字段
- 有可读的导出结果，便于人工检查
- 有基本校验机制，能发现明显异常
- 后续阶段可以只依赖快照接口，而不直接依赖游戏内部对象

## 9. 阶段二建议起步顺序

既然阶段一已经收口，下一轮编码建议按这个顺序做:

1. 先把 `actions` 中已经稳定的动作名映射到真正的执行器
2. 再打通战斗内 `play_card / use_potion / discard_potion / end_turn`
3. 然后补地图、奖励、事件、商店、篝火、宝箱这些局外动作
4. 让每个动作执行后都配套一条 `delta observation`
5. 最后再处理多步交互和失败恢复

## 10. 和后续阶段的边界

阶段一输出的是“看见了什么”，不是“要做什么”。

所以这一阶段的代码应避免:

- 在快照构建过程中夹带策略判断
- 直接在 Hook 里写自动出牌逻辑
- 让调试导出和动作执行强耦合

只要把状态层做干净，阶段二接动作系统、阶段三接规划系统都会轻松很多。
