# 阶段二 TODO：动作层 / Action Execution

## 1. 目标

阶段二的目标不是让 AI 立刻“很会玩”，而是先让它可以稳定、合法地操作游戏。

阶段二要建立的闭环是:

1. 先通过阶段一接口读取 `context`
2. 再读取当前屏幕的 `actions`
3. 选择一个动作并执行
4. 用 `delta observation` 回读执行结果
5. 如果动作失败或界面切换，再进入恢复逻辑

## 2. 当前输入契约

阶段二默认依赖下面这些阶段一接口:

- `context`
- `compact observation`
- `actions`
- `observation/delta`
- `knowledge/current`

它们现在已经足够让动作层避免直接依赖大部分原始游戏对象。

## 3. 优先级

### P2-1 战斗动作闭环

先打通:

- `play_card`
- `use_potion`
- `discard_potion`
- `end_turn`

要求:

- 能按 `actions` 中的参数结构执行
- 能处理合法目标选择
- 执行后能通过 `delta observation` 看出结果

### P2-2 局外单步选择

再打通:

- `choose_map_node`
- `choose_event_option`
- `choose_rest_option`
- `shop_purchase`
- `claim_reward`
- `select_card_reward`
- `claim_treasure_relic`
- `proceed`

要求:

- 输入参数与 `actions` 返回保持一致
- 出错时能给出明确失败原因

已确认 bug:

- 宝箱房在“箱子尚未被玩家点开”的初始状态下，`context` 会长期停留在 `stateType=treasure` + `isStable=false` + `isTransitioning=true`
- 此时 `/api/v1/actions` 为空，`/api/v1/treasure` 返回 `canProceed=false` 且 `relics=[]`
- 实际游戏界面并不是短暂过场，而是在等待玩家点击箱子本体；当前 CLI 没有暴露这个动作
- 结果是 agent 会把未开箱状态误判成“仍在 transition”，持续等待而无法推进整局
- 战斗中一旦打出手牌，剩余手牌的顺序和 `actions` 返回的 `index` 可能立刻变化
- agent 如果复用上一次读取到的旧 `index` 连续出牌，就可能把下一张牌打错，甚至把攻击误发成防御或反过来
- 这个问题不是偶发读取抖动，而是当前“按 index 执行”的天然风险，在多步战斗回合里会稳定出现

当前状态:

- 宝箱未开箱问题：`已完成`
- 战斗旧 index 误出牌的执行层防护：`已完成（通过可选 expected_* 校验先做 fail-fast）`

修复建议:

- 为宝箱房补一个“打开箱子 / 点击箱体”的显式动作，先把房间从未展开状态切到 relic 可选状态
- 或者至少把该状态单独标成可行动的等待输入态，而不是继续复用 `isTransitioning=true`
- 在 `wait room-ready` 和房间流转文档里把这个场景列为已知例外，避免 agent 无限等待
- 战斗动作不要只靠瞬时 `index` 作为唯一标识，至少要补一个更稳定的卡牌定位方式
- 可选方案包括：给动作面暴露稳定的 hand instance id，或在执行层支持按 `card_id + title + cost + legalTargets` 做二次匹配校验
- 在更稳的执行契约落地前，agent 侧必须遵守“每出一张牌后重新读取 `actions` / `combat hand`，禁止复用旧 index 连打”

### P2-3 多步交互

最后处理:

- `select_card`
- `confirm_selection`
- `cancel_selection`
- `skip_selection`
- 需要二次确认或多步选取的战斗效果

要求:

- 明确当前交互模式
- 避免动作发到错误 screen
- 动作失败后能恢复到可读状态

## 4. 核心任务

### 4.1 定义动作执行 API

需要确定:

- 本地 CLI 入口怎么调用动作
- server 是否直接提供写接口
- 参数格式是否复用 `actions` 的结构

建议:

- 先做独立的本地执行入口
- 参数命名完全复用 `actions`
- 不引入第二套动作命名

当前仓库已落地的起步实现:

- server 写接口: `POST /api/v1/actions/execute`
- CLI 执行入口: `./sts exec ACTION [INDEX] [TARGET]`
- 执行响应会直接返回一条关联的 `delta observation`
- 当前 canonical 参数收敛为 `action + index? + target?`
- 旧字段名仍兼容，但不再是推荐写法

### 4.2 做合法性检查

每个动作执行前要检查:

- 当前 `stateType` 是否匹配
- 索引是否仍然有效
- target 是否仍然存在
- 玩家是否还能操作
- 当前 screen 是否允许该动作

### 4.3 做动作结果回读

每个动作执行后至少要记录:

- 请求的动作名和参数
- 是否执行成功
- 如果失败，失败原因
- 对应的 `delta observation`
- 如有必要，落一份完整调试快照

### 4.4 做恢复策略

需要覆盖:

- 动作执行时屏幕刚切换
- 目标在动画过程中失效
- 手里牌顺序变化导致索引失效
- 需要 confirm 但模型直接发了 proceed

恢复策略至少包括:

- 重新读 `context`
- 重新读 `actions`
- 判断是否需要重试
- 明确哪些错误禁止自动重试

### 4.5 做更高层 CLI 聚合

当前状态: `已完成（第一批）`

当前 CLI 的底层能力够用，但 agent 实战里仍然显得过碎，尤其是战斗读路径。

建议区分两类聚合:

- 查询层聚合: 应该更积极补高层命令
- 执行层聚合: 只能在中间状态可安全重解析时再做

优先建议补的高层读命令:

- `sts combat snapshot`
- `sts room snapshot --detail ...`

目标:

- 一次返回 `context + actions + combat summary + enemies + hand + player summary`
- 让 agent 在战斗回合开始时尽量只做一次读，而不是重复调用多个窄接口
- 保留当前细粒度 endpoint 作为底层能力和调试能力，而不是强迫 agent 总是手工拼装

当前已落地:

- `sts combat snapshot`
- `sts room snapshot --detail standard|full`
- `sts card-reward skip`

可以安全聚合的执行命令:

- `continue_game`
- `proceed`
- `choose_map_node`
- `rewards claim-all-safe`
- 未来的 `rest auto` / `treasure auto-open` 这类确定性局外流程

不应在当前契约下盲目聚合的执行命令:

- 战斗内多张牌连续打出的“脚本式连打”

原因:

- 战斗动作目前主要还是 `action + index + target` 契约
- 一旦打出一张牌，剩余手牌顺序和 `actions` 返回的 `index` 会变化
- 如果多步命令内部只是复用同一份旧动作面，就会稳定地产生误出牌，而不是减少误操作

因此，只有在下面条件成立后，才适合做更高层的战斗多步命令:

- 执行器内部每一步都会重新读取当前动作面
- 或者动作面暴露稳定的 hand instance id
- 或者执行层能按更丰富的卡牌身份字段做二次匹配校验

### 4.6 增加可复盘的结构化日志

当前状态: `已完成（基础版）`

除了补高层命令，server 端或 CLI 端也应提供可控的执行日志，方便后期复盘、定位误操作，并据此优化 agent 策略和接口设计。

建议至少区分两档:

- 默认结构化日志: 面向日常复盘和问题定位
- verbose trace: 面向调试 wait、转场、动作失配和恢复逻辑

建议记录的核心字段:

- 时间戳
- run id / floor / room / stateType / isStable
- 本次读取或执行的 CLI 命令
- 读取到的关键动作面摘要，例如 `actions` 数量、hand 摘要、敌人摘要
- agent 最终选择的动作和参数，例如 `action + index + target`
- 动作前后的 observation version 或 `delta observation` 摘要
- 是否命中了 helper flow，例如 `proceed`、`continue_game`、未来的高层聚合命令
- 失败原因、恢复建议、调试快照路径

推荐额外补一个相关 id:

- 每个 agent 决策周期的 correlation id
- 每个多步 helper 命令的 step id 或 parent id

这样可以直接支持几类复盘:

- 为什么 agent 在某一拍选择了某个动作
- 误出牌时，当时看到的手牌索引和执行时的真实动作面是否已变化
- 某些房间是否存在明显的过度查询，适合作 `combat snapshot` / `room snapshot`
- wait 或转场失败时，错误是来自 server、CLI 还是游戏内状态机

建议边界:

- 默认日志不要无上限落完整 observation，避免噪声和体积失控
- 正常档优先记录结构化摘要，完整快照只在失败、显式 debug 或采样模式下保留
- CLI 日志和 server 日志最好能通过同一个 correlation id 关联

当前已落地:

- server 结构化执行日志 `action-execution.jsonl`
- 失败时调试快照落盘
- CLI `wait --verbose` trace
- CLI `exec` 自动附带 correlation id，server 端日志可关联

## 5. 建议实现顺序

1. 增加动作执行总入口
2. 先只支持战斗 `play_card` 和 `end_turn`
3. 再补 `use_potion` 和 `discard_potion`
4. 再补地图 / 奖励 / 事件 / 商店 / 篝火 / 宝箱
5. 最后补卡牌选择、多步交互、恢复逻辑

当前已完成到:

- 动作执行总入口
- 战斗 `play_card / use_potion / discard_potion / end_turn`
- 地图 / 奖励 / 事件 / 商店 / 篝火 / 宝箱 / 基础卡牌选择
- 战斗内 hand selection 的 `select_card / confirm_selection`
- 执行日志落盘并关联 `delta observation`
- 失败响应内建恢复建议和调试快照

当前仍待补强:

- 游戏内完整 smoke test 和边界动作回归
- 更稳定的手牌实例 id，而不是只靠 `index + expected_*`

## 6. 验收标准

满足下面条件，可以认为阶段二进入可用状态:

- AI 可以读取 `actions` 并成功执行至少一条动作
- 战斗里能连续打牌并结束回合
- 局外至少能完成地图选择、奖励选择和事件选择
- 每个动作执行后都能读到合理的 `delta observation`
- 常见非法动作不会导致状态机失控

## 6.1 当前结论

阶段二当前结论：`已完成`

依据：

- 已在真实游戏中完成完整战斗执行
- 已在真实游戏中完成奖励领取、卡牌选择和地图前进
- `CLI -> server -> execute -> delta` 闭环已实战验证
- 当前 canonical 参数已收敛为 `action + index? + target?`
- 执行失败时可返回恢复建议、当前动作面和调试快照路径

本阶段后续不再继续堆功能，后续工作转入阶段三规划层。

## 7. 非目标

阶段二当前不追求:

- 最优决策质量
- 长期路线规划
- 套牌构筑策略
- 跨楼层资源规划

这些属于阶段三。
