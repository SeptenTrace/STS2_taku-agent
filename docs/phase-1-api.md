# 阶段一 API 设计：低 Token 观察层

## 1. 目标

阶段一不直接追求“让 AI 操作游戏”，而是先把“让 AI 用尽可能少的上下文看懂整局游戏”做扎实。

因此阶段一 API 的核心目标是：

- 让模型先知道自己当前处在什么场景
- 让模型只查询它当前决策需要的信息
- 避免每一步都重复发送整份全量状态
- 把调试用途的全量快照和模型用途的紧凑观察层分开

## 1.1 当前已完成情况

目前阶段一已经完成的关键部分包括:

- `GameSnapshot` 统一状态容器
- `context` 场景识别接口
- `compact observation` 紧凑观察接口
- `delta observation` 增量观察接口
- 通用 `actions` 动作面接口
- 当前上下文 `knowledge/current` 知识缓存
- 细粒度只读 HTTP API
- 本地 `./sts` CLI
- 语义化战斗动作查询 `combat/actions`
- 轻量玩家摘要 `player/summary`
- 结构化战斗摘要 `combat/summary`

这说明阶段一已经从“单纯读取战斗快照”走到了“可被 agent 直接调用的整局低 token 观察接口”。

## 1.2 参考源与更远目标

`STS2MCP` 是非常好的参考源。

它帮助我们确认了:

- 哪些 Hook 可用
- 如何做 `stateType` 分类
- 如何在不同房间和 overlay 下拼接状态
- 哪些运行时对象可以安全读取

但当前仓库的目标更远一点:

- 不只是把游戏状态暴露出来
- 而是尽可能降低模型的 token 消耗

所以当前设计选择了:

- `CLI + server`
- `context -> compact observation -> narrow endpoint`
- 默认不走 `state/full`

这比单纯“提供完整状态 API”更偏向 agent 友好的查询设计。

## 2. 设计原则

### 2.1 先查上下文，再查细节

模型默认先查：

- `/api/v1/context`

它只返回：

- 当前 `stateType`
- 当前房间类型 / overlay 类型
- 推荐下一步查询哪些 endpoint

这一步的目标是避免模型一上来就读完整局状态。

### 2.2 提供紧凑观察层

模型第二步默认查：

- `/api/v1/observation/compact`

这个接口只返回：

- 当前场景的最小目标
- 当前最关键事实
- 当前最推荐的后续查询

它不是调试快照，而是面向推理的观察层。

### 2.3 细粒度查询优先于全量查询

例如战斗中不要默认读完整状态，而是按需查：

- `/api/v1/combat/summary`
- `/api/v1/actions`
- `/api/v1/combat/hand`
- `/api/v1/combat/enemies`
- `/api/v1/combat/piles`
- `/api/v1/knowledge/current`

同理，局外则按需查：

- 地图
- 事件
- 奖励
- 商店
- 营火
- 宝箱

### 2.4 `state/full` 仅用于调试

- `/api/v1/state/full`

这个接口保留给调试和排障使用，不应作为模型默认入口。

## 3. 数据分层

### 3.1 `GameSnapshot`

完整状态容器，用于 server 内部统一构建。

包含：

- `context`
- `run`
- `player`
- `compactObservation`
- `combat`
- `map`
- `rewards`
- `cardReward`
- `event`
- `shop`
- `restSite`
- `treasure`
- `cardSelection`
- `overlay`

### 3.2 `CompactObservation`

专门给模型的低 token 观察视图。

包含：

- `stateType`
- `goal`
- `facts`
- `suggestedQueries`

### 3.3 领域子快照

不同屏幕只暴露不同子快照，例如：

- 战斗：`CombatStateSnapshot`
- 地图：`MapStateSnapshot`
- 事件：`EventStateSnapshot`
- 商店：`ShopStateSnapshot`

## 4. 当前 API 目录

除了 HTTP API，仓库根目录还提供一个本地 CLI：

- `./sts`

它的目标不是替代 API，而是把最常用查询固化为固定命令，进一步降低 agent 的试错成本。

常用命令：

- `./sts ping`
- `./sts capabilities`
- `./sts context`
- `./sts next`
- `./sts combat actions`
- `./sts combat hand`
- `./sts combat enemies`
- `./sts player summary`
- `./sts player deck`
- `./sts get /api/v1/...`

其中：

- `./sts next`
  会合并查询 `context` 和 `compact observation`
- `./sts combat actions`
  会直接返回当前合法动作集
- `./sts get`
  允许在需要时访问任意底层 endpoint

### 通用入口

- `/api/v1/context`
- `/api/v1/observation/compact`
- `/api/v1/capabilities`
- `/api/v1/run`

### 玩家信息

- `/api/v1/player/summary`
- `/api/v1/player/deck`
- `/api/v1/player/relics`
- `/api/v1/player/potions`
- `/api/v1/player/status`

### 战斗信息

- `/api/v1/combat/summary`
- `/api/v1/combat/actions`
- `/api/v1/combat/hand`
- `/api/v1/combat/enemies`
- `/api/v1/combat/piles`

### 局外信息

- `/api/v1/map/summary`
- `/api/v1/event`
- `/api/v1/shop`
- `/api/v1/rest-site`
- `/api/v1/rewards`
- `/api/v1/card-reward`
- `/api/v1/card-selection`
- `/api/v1/treasure`

### 调试接口

- `/api/v1/state/full`

## 5. 模型推荐查询顺序

### 战斗中

1. `/api/v1/context`
2. `/api/v1/combat/summary`
3. `/api/v1/combat/actions`
4. `/api/v1/combat/enemies`
5. 如果需要卡牌文本或更多细节，再查 `/api/v1/combat/hand`
6. 如果必要，再查 `/api/v1/combat/piles` 或 `/api/v1/player/status`

### 地图上

1. `/api/v1/context`
2. `/api/v1/map/summary`
3. 如果需要构筑上下文，再查 `/api/v1/player/summary` 或 `/api/v1/player/deck`

### 奖励 / 商店 / 事件

1. `/api/v1/context`
2. 对应场景 endpoint
3. 只有在判断需要时，再查 `/api/v1/player/deck`、`/api/v1/player/relics`

## 6. 相比 STS2MCP 的改进方向

这套设计相对 `STS2MCP` 的主要改动不是“读取能力更强”，而是“默认观察成本更低”：

- 默认入口从全量状态改成 `context + compact observation`
- API 按决策场景切分，而不是按实现层切分
- 明确标出推荐查询顺序，降低模型试错成本
- `state/full` 降级为调试接口，不再鼓励默认使用

## 7. 当前阶段一边界

当前阶段一仍然是只读观察层，不包含：

- 自动出牌
- 自动结束回合
- 自动选图
- 自动领奖励

这些属于阶段二。

## 8. 接下来需要做什么

阶段一到这里已经完成。

接下来进入阶段二，重点会从“看懂”切到“能动”，但阶段二要直接建立在当前观察契约上:

- `context` 仍然是统一入口
- `actions` 提供当前屏幕的动作名和参数结构
- `delta observation` 提供动作执行后的最小回读
- `knowledge/current` 只在需要展开文本含义时调用

阶段二的具体拆分见 `docs/phase-2-todo.md`。
